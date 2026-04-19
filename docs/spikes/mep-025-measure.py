"""MEP-025 sample measurement harness.

Mirrors the existing C# ContainerReferenceDetector + UomNormalizationService
parse logic in Python so the Kaggle dataset can be sampled without standing
up the EF context. Results are projections — real numbers emerge during
MEP-026 implementation.

HOW TO REPRODUCE
================

1. Download the dataset (each user fetches their own copy per CC BY-NC-SA):
     https://www.kaggle.com/datasets/wilmerarltstrmberg/recipe-dataset-over-2m

2. Put the extracted CSV somewhere outside the repo, e.g.:
     /tmp/mep-025/recipes_data.csv

3. Extract the first 500 non-Recipes1M rows to JSON:
     python -c "
     import csv, sys, json
     csv.field_size_limit(10_000_000)
     reader = csv.DictReader(open('/tmp/mep-025/recipes_data.csv', encoding='utf-8'))
     out = []
     for row in reader:
         if row['source'] == 'Recipes1M': continue
         out.append(row)
         if len(out) >= 500: break
     json.dump(out, open('/tmp/mep-025/sample-500.json', 'w', encoding='utf-8'))
     "

4. Run this script, pointing SAMPLE_PATH at the JSON file:
     SAMPLE_PATH=/tmp/mep-025/sample-500.json python docs/spikes/mep-025-measure.py

Outputs three UOM-resolution scenarios (as-is, period-stripped, +pkg
container keyword), NER token counts, prose-filter retention, and a
storage projection to the 1.64M-row target.

DO NOT commit the extracted CSV or the sample-500.json to the repo --
both contain real recipe text and the dataset license prohibits
redistribution.
"""

import ast
import io
import json
import os
import re
import statistics
import sys
from collections import Counter

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

SAMPLE_PATH = os.environ.get('SAMPLE_PATH', r'C:\temp\mep-025\sample-500.json')

CONTAINER_KEYWORDS_BASE = [
    'bag', 'bottle', 'box', 'can', 'carton', 'jar', 'packet', 'tube'
]
CONTAINER_KEYWORDS_EXT = CONTAINER_KEYWORDS_BASE + ['pkg', 'package']

# Mirror of seeded UnitOfMeasure rows (Abbreviation + Name, lowercased).
KNOWN_UOM_TOKENS = {
    'ea', 'each',
    'g', 'gram',
    'ml', 'milliliter',
    'cup',
    'fl oz', 'fluid ounce',
    'l', 'liter',
    'pt', 'pint',
    'qt', 'quart',
    'tbsp', 'tablespoon',
    'tsp', 'teaspoon',
    'kg', 'kilogram',
    'lb', 'pound',
    'oz', 'ounce',
}

FIRST_PERSON = re.compile(r'\b(i|me|my|we|our|us|mine)\b', re.IGNORECASE)
PARENTHETICAL = re.compile(r'\([^)]{40,}\)')
IMPERATIVE_VERBS = {
    'add', 'bake', 'beat', 'blend', 'boil', 'break', 'bring', 'brown',
    'chill', 'chop', 'combine', 'cook', 'cool', 'cover', 'cream',
    'cut', 'dice', 'divide', 'drain', 'drop', 'dust', 'fold', 'form',
    'freeze', 'fry', 'garnish', 'grate', 'grease', 'grill', 'heat',
    'knead', 'layer', 'let', 'lift', 'line', 'make', 'mash', 'melt',
    'mix', 'note', 'peel', 'place', 'pour', 'preheat', 'prepare',
    'press', 'puree', 'put', 'reduce', 'refrigerate', 'remove',
    'repeat', 'return', 'roast', 'roll', 'rub', 'saute', 'saut\u00e9',
    'scoop', 'scrape', 'season', 'separate', 'serve', 'set', 'shape',
    'simmer', 'slice', 'soak', 'spoon', 'spread', 'sprinkle', 'stir',
    'strain', 'taste', 'top', 'toss', 'transfer', 'turn', 'unmold',
    'use', 'warm', 'wash', 'whisk', 'wrap'
}


def detect_container(text: str, keywords: list[str]) -> str | None:
    for kw in keywords:
        for form in (kw, kw + 's'):
            if re.search(rf'(?<![A-Za-z]){re.escape(form)}(?![A-Za-z.])',
                         text, re.IGNORECASE):
                return kw
            if re.search(rf'(?<![A-Za-z])\b{re.escape(form)}\.\b',
                         text, re.IGNORECASE):
                return kw
    return None


def parse_measure(s: str) -> tuple[float, str]:
    s = s.strip()
    i = 0
    while i < len(s) and (s[i].isdigit() or s[i] in './'):
        i += 1
    if i == 0:
        return (0.0, s)
    num_part = s[:i]
    remainder = s[i:].strip()
    if '/' in num_part:
        parts = num_part.split('/')
        if len(parts) == 2:
            try:
                n, d = float(parts[0]), float(parts[1])
                if d != 0:
                    return (n / d, remainder)
            except ValueError:
                return (0.0, remainder)
        return (0.0, remainder)
    try:
        return (float(num_part), remainder)
    except ValueError:
        return (0.0, remainder)


def normalize_uom_token(remainder: str, strip_periods: bool) -> str | None:
    if not remainder:
        return None
    tokens = remainder.lower().strip().split()
    if strip_periods:
        tokens = [t.rstrip('.') for t in tokens]
    candidates = []
    for n in (3, 2, 1):
        if len(tokens) >= n:
            joined = ' '.join(tokens[:n])
            candidates.append(joined)
            if joined.endswith('s'):
                candidates.append(joined[:-1])
    for c in candidates:
        if c in KNOWN_UOM_TOKENS:
            return c
    return None


def prose_filter(steps: list[str]) -> tuple[int, int]:
    retained = 0
    for step in steps:
        if not step or not step.strip():
            continue
        cleaned = PARENTHETICAL.sub('', step)
        words = cleaned.split()
        if len(words) > 40:
            continue
        if FIRST_PERSON.search(cleaned):
            continue
        first_word = re.sub(r'[^A-Za-z\-]', '',
                            words[0] if words else '').lower()
        if first_word not in IMPERATIVE_VERBS:
            continue
        retained += 1
    return (retained, len(steps))


def measure(sample: list[dict], scenario: str,
            container_keywords: list[str],
            strip_periods: bool) -> dict:
    total = 0
    flagged = 0
    flagged_recipes = 0
    hits = Counter()
    resolved = 0
    claude = 0
    no_qty = 0
    for row in sample:
        try:
            ingr = ast.literal_eval(row['ingredients'])
        except (ValueError, SyntaxError):
            continue
        has_flag = False
        for s in ingr:
            total += 1
            kw = detect_container(s, container_keywords)
            if kw:
                flagged += 1
                hits[kw] += 1
                has_flag = True
                continue
            qty, remainder = parse_measure(s)
            if qty == 0 and not remainder:
                no_qty += 1
                continue
            if qty == 0:
                claude += 1
                continue
            tok = normalize_uom_token(remainder, strip_periods)
            if tok:
                resolved += 1
            else:
                claude += 1
        if has_flag:
            flagged_recipes += 1
    return dict(scenario=scenario, total=total, flagged=flagged,
                flagged_recipes=flagged_recipes, hits=dict(hits),
                resolved=resolved, claude=claude, no_qty=no_qty)


def main():
    with open(SAMPLE_PATH, encoding='utf-8') as f:
        sample = json.load(f)

    print(f'MEP-025 sample measurement -- {len(sample)} recipes')
    print()

    scenarios = [
        ('A. As-is (current pipeline)', CONTAINER_KEYWORDS_BASE, False),
        ('B. With period-stripping', CONTAINER_KEYWORDS_BASE, True),
        ('C. B + "pkg"/"package" container keywords', CONTAINER_KEYWORDS_EXT, True),
    ]
    for name, kws, strip in scenarios:
        r = measure(sample, name, kws, strip)
        uom_total = r['resolved'] + r['claude'] + r['no_qty']
        print('=' * 66)
        print(name)
        print('=' * 66)
        print(f'  Ingredients:                 {r["total"]}')
        print(f'  Container-flagged:           {r["flagged"]} ({r["flagged"]/r["total"]*100:.1f}%)')
        print(f'  Recipes w/ >=1 flag:         {r["flagged_recipes"]}/{len(sample)} ({r["flagged_recipes"]/len(sample)*100:.1f}%)')
        print(f'  Keyword hits:                {r["hits"]}')
        print(f'  Deterministic UOM resolve:   {r["resolved"]} ({r["resolved"]/uom_total*100:.1f}% of non-container)')
        print(f'  Falls through to Claude:     {r["claude"]} ({r["claude"]/uom_total*100:.1f}%)')
        print(f'  No numeric quantity:         {r["no_qty"]} ({r["no_qty"]/uom_total*100:.1f}%)')
        print()

    # ── Vocabulary / prose / storage ─────────────────────────────────────
    ner_tokens = Counter()
    title_chars = ingredient_chars = direction_chars = 0
    step_counts = []
    ingredient_counts = []
    prose_ratios = []

    for row in sample:
        try:
            ingr = ast.literal_eval(row['ingredients'])
            ner = ast.literal_eval(row['NER'])
            steps = ast.literal_eval(row['directions'])
        except (ValueError, SyntaxError):
            continue
        for t in ner:
            ner_tokens[t.lower().strip()] += 1
        title_chars += len(row.get('title', ''))
        ingredient_chars += sum(len(s) for s in ingr)
        direction_chars += sum(len(s) for s in steps)
        step_counts.append(len(steps))
        ingredient_counts.append(len(ingr))
        retained, original = prose_filter(steps)
        if original > 0:
            prose_ratios.append(retained / original)

    print('=' * 66)
    print('NER -> CanonicalIngredient')
    print('=' * 66)
    print(f'  Total NER occurrences:       {sum(ner_tokens.values())}')
    print(f'  Unique tokens in sample:     {len(ner_tokens)}')
    print(f'  Top 10:')
    for tok, n in ner_tokens.most_common(10):
        print(f'    {n:>4}  {tok}')
    print()

    print('=' * 66)
    print('Prose filter')
    print('=' * 66)
    if prose_ratios:
        print(f'  Mean retention:              {statistics.mean(prose_ratios)*100:.1f}%')
        print(f'  Median retention:            {statistics.median(prose_ratios)*100:.1f}%')
        low = sum(1 for r in prose_ratios if r < 0.8)
        print(f'  Recipes below 80% retention: {low}/{len(prose_ratios)} ({low/len(prose_ratios)*100:.1f}%)')
    print()

    print('=' * 66)
    print('Storage projection (1,643,098 usable rows)')
    print('=' * 66)
    per = (title_chars + ingredient_chars + direction_chars) / len(sample)
    usable = 1_643_098
    raw_bytes = per * usable
    ing_rows = statistics.mean(ingredient_counts) * usable
    print(f'  Avg recipe payload:          {per:.1f} bytes')
    print(f'  Raw text @ 1.64M rows:       ~{raw_bytes/1_073_741_824:.2f} GB')
    print(f'  RecipeIngredient rows:       ~{ing_rows/1_000_000:.2f}M')
    print(f'  Postgres footprint estimate: ~{raw_bytes*2/1_073_741_824:.1f}-{raw_bytes*3.5/1_073_741_824:.1f} GB')


if __name__ == '__main__':
    main()
