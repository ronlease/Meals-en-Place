# Meals en Place -- Product Backlog

## [MEP-001] Inventory Management

**Status:** Done
**Priority:** High

### Business Problem
I need a single place to track every food item I have on hand across my pantry, fridge, and freezer. Without this, I cannot know what ingredients are available for cooking, which items are approaching expiry, or what I need to buy. Each item must record its storage location, quantity, unit of measure, and an optional expiry date so that downstream features (recipe matching, waste reduction, shopping lists) have accurate data to work with.

### Acceptance Criteria
```gherkin
Feature: Inventory Management

  Scenario: Add an item to inventory
    Given I am on the inventory management screen
    When I add an item with name "Chicken Breast", location "Freezer", quantity 2, unit "lb", and expiry date "2026-04-01"
    Then the item "Chicken Breast" appears in my Freezer inventory
    And the item shows quantity 2, unit "lb", and expiry "2026-04-01"

  Scenario: Add an item without an expiry date
    Given I am on the inventory management screen
    When I add an item with name "Olive Oil", location "Pantry", quantity 500, and unit "ml"
    And I leave the expiry date blank
    Then the item "Olive Oil" appears in my Pantry inventory
    And the expiry date field is empty

  Scenario: Edit an existing inventory item
    Given I have an item "Eggs" in my Fridge with quantity 12 and unit "each"
    When I edit the item to change the quantity to 6
    Then the item "Eggs" shows quantity 6

  Scenario: Change an item's storage location
    Given I have an item "Ground Beef" in my Fridge with quantity 1 and unit "lb"
    When I edit the item to change the location to "Freezer"
    Then the item "Ground Beef" appears in my Freezer inventory
    And the item no longer appears in my Fridge inventory

  Scenario: Remove an item from inventory
    Given I have an item "Expired Yogurt" in my Fridge
    When I remove the item "Expired Yogurt"
    Then the item "Expired Yogurt" no longer appears in any inventory location

  Scenario: View inventory filtered by location
    Given I have items in Pantry, Fridge, and Freezer
    When I filter inventory by location "Fridge"
    Then only items stored in the Fridge are displayed
```

---

## [MEP-002] UOM Normalization

**Status:** Done
**Priority:** High

### Business Problem
Recipes and inventory entries use inconsistent units of measure. A recipe might call for "2 cups" of flour while my inventory records flour in "grams." Colloquial expressions like "a knob of butter" or "1 head of garlic" also appear in recipe sources. Without normalizing these to canonical units with known conversion factors, the system cannot accurately determine whether I have enough of an ingredient. Claude resolves ambiguous or colloquial units; deterministic conversions handle standard units.

### Acceptance Criteria
```gherkin
Feature: UOM Normalization

  Scenario: Convert between compatible standard units
    Given an inventory item "All-Purpose Flour" with quantity 500 and unit "g"
    And a recipe requires 2 cups of "All-Purpose Flour"
    When the system normalizes both quantities to metric base units
    Then the inventory quantity converts to 500 g
    And the recipe quantity converts to approximately 250 g
    And the system can compare the two quantities

  Scenario: Resolve a colloquial unit via Claude
    Given a recipe ingredient specifies "a knob of butter"
    When the system encounters the unit "knob"
    Then Claude resolves "a knob of butter" to approximately 15 g
    And the resolved quantity and unit are stored for matching

  Scenario: Resolve a count-based colloquial unit via Claude
    Given a recipe ingredient specifies "1 head of garlic"
    When the system encounters the unit "head"
    Then Claude resolves "1 head of garlic" to a canonical quantity and unit
    And the resolved quantity and unit are stored for matching

  Scenario: Reject incompatible unit conversion
    Given an inventory item measured in "ml"
    And a recipe ingredient measured in "g"
    And the two ingredients are not the same type (volume vs weight)
    When the system attempts to compare quantities
    Then the system reports that the units are incompatible
    And no conversion is performed

  Scenario: Standard unit conversion without Claude
    Given a recipe requires 8 "oz" of cheddar cheese
    When the system normalizes the quantity
    Then the quantity converts to approximately 226.8 g deterministically
    And Claude is not invoked
```

---

## [MEP-003] Container Reference Resolution

**Status:** Done
**Priority:** High

### Business Problem
Many recipes and inventory entries specify quantities by container rather than by unit of measure -- "1 can of diced tomatoes," "1 jar of marinara sauce," "1 box of pasta." Container sizes vary between brands and change over time due to shrinkflation. If the system assumes a container size from a lookup table, the math will silently drift out of accuracy as products change. I need the system to detect container references, flag them, and require me to declare the actual net weight or volume before any quantity math runs. This keeps matching calculations honest.

### Acceptance Criteria
```gherkin
Feature: Container Reference Resolution

  Scenario: Detect a container reference on inventory entry
    Given I am adding an item to my inventory
    When I enter "1 can of diced tomatoes"
    Then the system detects "can" as a container reference
    And the system prompts me to declare the net weight or volume

  Scenario: Declare container size for an inventory item
    Given the system has flagged "1 can of diced tomatoes" as a container reference
    When I declare the net weight as 14.5 oz
    Then the item is stored with quantity 14.5, unit "oz"
    And the original entry "1 can of diced tomatoes" is preserved in the Notes field

  Scenario: Detect a container reference on recipe import
    Given I am importing a recipe that includes "1 can chopped tomatoes"
    When the system parses the ingredient list
    Then the system detects "can" as a container reference
    And the recipe ingredient is flagged as unresolved

  Scenario: Resolve a container reference on a recipe ingredient
    Given a recipe has an unresolved container reference "1 jar marinara sauce"
    When I declare the net volume as 24 oz
    Then the recipe ingredient stores quantity 24, unit "oz"
    And the Notes field preserves "1 jar marinara sauce"
    And the recipe ingredient is marked as resolved

  Scenario: Block matching for recipes with unresolved container references
    Given a recipe has one or more unresolved container references
    When the system runs recipe matching
    Then the recipe is excluded from match scoring
    And the recipe displays an "Awaiting Resolution" badge

  Scenario: Recipe enters matching pool after full resolution
    Given a recipe had unresolved container references
    And I have now declared sizes for all container references in the recipe
    When the system runs recipe matching
    Then the recipe is included in match scoring

  Scenario: Recognize multiple container keywords
    Given a recipe ingredient specifies "1 packet of taco seasoning"
    When the system parses the ingredient
    Then the system detects "packet" as a container reference
    And the ingredient is flagged for user declaration
```

---

## [MEP-004] Recipe Library Import

**Status:** Done (superseded by MEP-026 / MEP-033)
**Priority:** High

> **Supersession note:** The historical outcome stands — this story shipped with a TheMealDB-backed search/import flow. MEP-025 later established that TheMealDB's ~600-recipe catalog was the gating constraint on recipe matching, MEP-026 ingested the 1.64M-recipe Kaggle corpus offline to replace it, and MEP-033 then removed the TheMealDB integration surface entirely. The local recipe library outcome is still satisfied; the implementation source simply moved from a live third-party API to an offline bulk-ingest tool.

### Business Problem
I need a way to build my local recipe library without manually entering every recipe. TheMealDB provides free, open recipe data that I can search by term, cuisine, or category. Imported recipes are stored locally so I am not dependent on the external API for day-to-day use. Recipes with unresolved container references must be flagged and excluded from matching until I have declared all container sizes, ensuring that only fully resolved recipes participate in meal planning math.

### Acceptance Criteria
```gherkin
Feature: Recipe Library Import

  Scenario: Search TheMealDB by search term
    Given I am on the recipe import screen
    When I search for "chicken"
    Then the system queries TheMealDB for recipes matching "chicken"
    And the results are displayed for selection

  Scenario: Search TheMealDB by cuisine
    Given I am on the recipe import screen
    When I filter by cuisine "Italian"
    Then the system queries TheMealDB for Italian recipes
    And the results are displayed for selection

  Scenario: Search TheMealDB by category
    Given I am on the recipe import screen
    When I filter by category "Seafood"
    Then the system queries TheMealDB for Seafood recipes
    And the results are displayed for selection

  Scenario: Import a recipe with no container references
    Given I have selected a recipe from the search results
    And the recipe contains no container references in its ingredients
    When I confirm the import
    Then the recipe is stored locally with title, ingredients, instructions, and cuisine
    And the recipe is immediately available for matching

  Scenario: Import a recipe with container references
    Given I have selected a recipe from the search results
    And the recipe contains ingredients with container references
    When I confirm the import
    Then the recipe is stored locally
    And each container reference is flagged as unresolved
    And the recipe displays an "Awaiting Resolution" badge
    And the recipe is excluded from matching until all references are resolved

  Scenario: Avoid duplicate imports
    Given I have already imported recipe "Chicken Tikka Masala" from TheMealDB
    When I attempt to import the same recipe again
    Then the system notifies me that the recipe already exists locally
    And no duplicate is created

  Scenario: Handle TheMealDB API unavailability
    Given the TheMealDB API is unreachable
    When I attempt to search for recipes
    Then the system displays an error message indicating the service is unavailable
    And my existing local recipe library remains accessible
```

---

## [MEP-005] Recipe Dietary Classification

**Status:** Done
**Priority:** High

### Business Problem
I follow different dietary preferences at different times and want to filter my recipe library by dietary category. Manually tagging every imported recipe is tedious and error-prone. Claude can analyze a recipe's ingredient list and instructions to automatically classify it with applicable dietary labels -- Vegetarian, Vegan, Carnivore, LowCarb, GlutenFree, DairyFree. A recipe may carry multiple tags. This saves me time and ensures consistent classification across my library.

### Acceptance Criteria
```gherkin
Feature: Recipe Dietary Classification

  Scenario: Classify a vegetarian recipe
    Given a recipe contains no meat, poultry, or fish ingredients
    When Claude analyzes the recipe
    Then the recipe is tagged "Vegetarian"

  Scenario: Classify a vegan recipe
    Given a recipe contains no animal products of any kind
    When Claude analyzes the recipe
    Then the recipe is tagged "Vegan"
    And the recipe is also tagged "Vegetarian"
    And the recipe is also tagged "DairyFree"

  Scenario: Classify a recipe with multiple dietary tags
    Given a recipe contains only vegetables, grains, and plant-based fats
    And the recipe uses no gluten-containing grains
    When Claude analyzes the recipe
    Then the recipe is tagged "Vegan", "Vegetarian", "GlutenFree", and "DairyFree"

  Scenario: Classify a carnivore recipe
    Given a recipe is primarily composed of meat, fish, or animal products
    When Claude analyzes the recipe
    Then the recipe is tagged "Carnivore"

  Scenario: Classify on import
    Given I have just imported a recipe from TheMealDB
    When the import is complete
    Then Claude is invoked to classify the recipe
    And the resulting dietary tags are stored with the recipe

  Scenario: Filter recipe library by dietary tag
    Given my recipe library contains recipes with various dietary tags
    When I filter by "GlutenFree"
    Then only recipes tagged "GlutenFree" are displayed
```

---

## [MEP-006] Recipe Matching -- What Can I Make?

**Status:** Done
**Priority:** High

### Business Problem
The core value of this application is answering "what can I make right now?" based on what I already have on hand. The system needs to compare my current inventory against every fully-resolved recipe in my library, rank them by ingredient coverage, and present results in tiers: Full Match (I have everything), Near Match (missing one or two ingredients, with substitution suggestions), and Partial Match (I have some ingredients). Recipes with unresolved container references are excluded. Items approaching expiry should receive a scoring bonus to prioritize waste reduction.

### Acceptance Criteria
```gherkin
Feature: Recipe Matching

  Scenario: Full match
    Given I have all ingredients for recipe "Spaghetti Aglio e Olio" in my inventory
    And all quantities meet or exceed the recipe requirements
    When I request "What can I make?"
    Then "Spaghetti Aglio e Olio" appears in the Full Match tier

  Scenario: Near match with substitution suggestion
    Given I have all ingredients for recipe "Caesar Salad" except "anchovies"
    When I request "What can I make?"
    Then "Caesar Salad" appears in the Near Match tier
    And Claude suggests a substitution for "anchovies"

  Scenario: Partial match
    Given I have 3 out of 8 ingredients for recipe "Beef Stew"
    When I request "What can I make?"
    Then "Beef Stew" appears in the Partial Match tier
    And the match score reflects 3 out of 8 ingredients

  Scenario: Exclude unresolved recipes
    Given a recipe "Chili Con Carne" has unresolved container references
    When I request "What can I make?"
    Then "Chili Con Carne" does not appear in any match tier

  Scenario: Expiry-imminent items boost match score
    Given I have "Heavy Cream" expiring in 2 days
    And recipe "Alfredo Pasta" uses "Heavy Cream"
    When I request "What can I make?"
    Then "Alfredo Pasta" receives a scoring bonus for using an expiry-imminent item

  Scenario: Filter matches by dietary tag
    Given I request "What can I make?" with dietary filter "Vegetarian"
    When the system returns results
    Then only recipes tagged "Vegetarian" appear in the results

  Scenario: Filter matches by cuisine
    Given I request "What can I make?" with cuisine filter "Mexican"
    When the system returns results
    Then only recipes with cuisine "Mexican" appear in the results

  Scenario: UOM normalization during matching
    Given my inventory has "Butter" recorded as 227 g
    And a recipe requires 1 cup of "Butter"
    When the system runs matching
    Then the system converts both to metric base units for comparison
    And accurately determines whether I have enough
```

---

## [MEP-007] Meal Plan Generation

**Status:** Done
**Priority:** High

### Business Problem
Planning meals for the week by hand is time-consuming, and I tend to repeat the same few dishes or let ingredients go to waste. I need the system to generate a weekly meal plan that optimizes for waste reduction (prioritize ingredients approaching expiry), seasonal produce affinity, dietary preferences, and variety (avoid repeating the same recipe within seven days). Claude reviews the full candidate set to balance these factors. After generation, I can manually swap individual slots to adjust the plan to my preferences.

### Acceptance Criteria
```gherkin
Feature: Meal Plan Generation

  Scenario: Generate a weekly meal plan
    Given I have recipes in my library and items in my inventory
    When I request a meal plan for the current week
    Then the system generates a plan assigning recipes to day/slot combinations
    And the plan covers the requested date range

  Scenario: Prioritize expiry-imminent ingredients
    Given I have "Salmon" in my fridge expiring in 3 days
    And a recipe "Grilled Salmon" uses "Salmon"
    When the system generates a meal plan
    Then "Grilled Salmon" is prioritized for an early slot in the plan

  Scenario: Avoid repeating recipes within seven days
    Given a meal plan is being generated for 7 days
    When the system assigns recipes to slots
    Then no recipe appears more than once in the plan

  Scenario: Respect dietary filter preferences
    Given I request a meal plan with dietary filter "Vegetarian"
    When the system generates the plan
    Then all assigned recipes are tagged "Vegetarian"

  Scenario: Incorporate seasonal produce affinity
    Given it is March and asparagus is in season for Zone 7a
    And a recipe "Roasted Asparagus Salad" uses asparagus
    When the system generates a meal plan
    Then "Roasted Asparagus Salad" receives a seasonal affinity bonus in ranking

  Scenario: Manually swap a meal plan slot
    Given a meal plan has been generated
    And "Pasta Primavera" is assigned to Wednesday Dinner
    When I swap Wednesday Dinner to "Mushroom Risotto"
    Then Wednesday Dinner now shows "Mushroom Risotto"
    And the original assignment is replaced

  Scenario: Claude reviews the plan for variety and waste optimization
    Given the system has ranked candidate recipes for each slot
    When Claude is invoked to review the full candidate set
    Then Claude returns an optimized plan balancing variety, waste reduction, and seasonal affinity
```

---

## [MEP-008] Shopping List Derivation

**Status:** Done
**Priority:** High

### Business Problem
Once I have a meal plan, I need to know exactly what to buy. The system should compare the meal plan's total ingredient requirements against my current inventory and generate a shopping list of items that are absent or present in insufficient quantity. This eliminates guesswork at the grocery store and prevents me from buying duplicates of items I already have.

### Acceptance Criteria
```gherkin
Feature: Shopping List Derivation

  Scenario: Generate shopping list from active meal plan
    Given I have an active meal plan for the current week
    And the meal plan requires ingredients not fully covered by my inventory
    When I generate a shopping list
    Then the list contains each missing or insufficient ingredient
    And each item shows the required quantity and unit

  Scenario: Exclude ingredients already in sufficient quantity
    Given my inventory has 1000 g of "All-Purpose Flour"
    And the meal plan requires 500 g of "All-Purpose Flour"
    When I generate a shopping list
    Then "All-Purpose Flour" does not appear on the shopping list

  Scenario: Show deficit quantity for partially covered ingredients
    Given my inventory has 100 g of "Cheddar Cheese"
    And the meal plan requires 300 g of "Cheddar Cheese"
    When I generate a shopping list
    Then "Cheddar Cheese" appears on the shopping list with quantity 200 g

  Scenario: Aggregate ingredient needs across multiple recipes
    Given the meal plan includes 3 recipes that each require "Olive Oil"
    And the total required is 90 ml
    And my inventory has 50 ml of "Olive Oil"
    When I generate a shopping list
    Then "Olive Oil" appears on the shopping list with quantity 40 ml

  Scenario: No shopping list without an active meal plan
    Given I do not have an active meal plan
    When I attempt to generate a shopping list
    Then the system informs me that a meal plan is required first
```

---

## [MEP-009] Waste Reduction Alerts

**Status:** Done
**Priority:** High

### Business Problem
Food waste is a major pain point. When items in my inventory are approaching their expiry date and I have recipes that can use them, I want the system to proactively alert me. This gives me time to cook with those ingredients before they spoil, saving money and reducing waste. Alerts should only fire when at least one available recipe can use the expiring item, so I receive actionable suggestions rather than noise.

### Acceptance Criteria
```gherkin
Feature: Waste Reduction Alerts

  Scenario: Alert for an expiry-imminent item with a matching recipe
    Given I have "Greek Yogurt" in my fridge expiring in 2 days
    And a recipe "Yogurt Parfait" uses "Greek Yogurt"
    And "Yogurt Parfait" is fully resolved
    When the system evaluates waste alerts
    Then a WasteAlert is surfaced for "Greek Yogurt"
    And the alert references "Yogurt Parfait" as a suggested recipe

  Scenario: No alert when no recipe matches the expiring item
    Given I have "Specialty Sauce" in my fridge expiring in 1 day
    And no recipe in my library uses "Specialty Sauce"
    When the system evaluates waste alerts
    Then no WasteAlert is surfaced for "Specialty Sauce"

  Scenario: No alert for items not near expiry
    Given I have "Butter" in my fridge expiring in 30 days
    When the system evaluates waste alerts
    Then no WasteAlert is surfaced for "Butter"

  Scenario: Multiple recipes suggested for one expiring item
    Given I have "Heavy Cream" in my fridge expiring in 3 days
    And recipes "Alfredo Pasta" and "Cream of Mushroom Soup" both use "Heavy Cream"
    And both recipes are fully resolved
    When the system evaluates waste alerts
    Then a WasteAlert is surfaced for "Heavy Cream"
    And the alert references both "Alfredo Pasta" and "Cream of Mushroom Soup"

  Scenario: No alert for items without an expiry date
    Given I have "Salt" in my pantry with no expiry date set
    When the system evaluates waste alerts
    Then no WasteAlert is surfaced for "Salt"
```

---

## [MEP-010] Seasonal Produce Guidance

**Status:** Done
**Priority:** High

### Business Problem
Cooking with seasonal produce means better flavor, lower cost, and supporting local agriculture. I want to see what produce is currently in season for my area (USDA Zone 7a, York, PA) and optionally filter recipe matching results to favor recipes that use seasonal ingredients. This helps me plan meals that align with what is fresh and available at local markets.

### Acceptance Criteria
```gherkin
Feature: Seasonal Produce Guidance

  Scenario: View currently in-season produce
    Given the current date is in March
    And the system has seasonality data for USDA Zone 7a
    When I view the seasonal produce list
    Then I see produce items whose peak season includes March
    And each item shows its full season date range

  Scenario: Filter recipe matching by seasonal ingredients
    Given I request "What can I make?" with the seasonal filter enabled
    When the system runs recipe matching
    Then recipes using currently in-season ingredients receive a ranking bonus
    And the results indicate which ingredients are in season

  Scenario: Produce out of season is not listed
    Given the current date is in March
    And "Peaches" are in season from June through August
    When I view the seasonal produce list
    Then "Peaches" do not appear in the list

  Scenario: Seasonality data scoped to Zone 7a
    Given the system has seasonality windows defined
    When I view the seasonal produce list
    Then the data reflects USDA Zone 7a growing seasons
    And the zone is displayed as context for the user
```

---

## [MEP-011] Metric Display

**Status:** Done
**Priority:** Low

### Business Problem
I may prefer to see quantities displayed in metric units rather than the default Imperial. The database already stores all quantities in metric base units internally, and the schema field for display preference is stubbed. This item covers implementing the UI toggle and the display-layer conversion so that all quantities render in the user's chosen system.

**Deferred Reason:** No blocking dependency. Simply post-MVP scope. The `DisplaySystem` preference field is already stubbed in the schema to avoid a future migration.

### Acceptance Criteria
```gherkin
Feature: Metric Display

  Scenario: Toggle display to metric
    Given my display preference is set to "Imperial"
    When I change my display preference to "Metric"
    Then all quantities in the UI render in metric units
    And the stored values remain unchanged

  Scenario: Toggle display to imperial
    Given my display preference is set to "Metric"
    When I change my display preference to "Imperial"
    Then all quantities in the UI render in imperial units
    And the stored values remain unchanged

  Scenario: Default display is imperial
    Given I have not set a display preference
    When I view any quantity in the UI
    Then quantities render in imperial units by default
```

---

## [MEP-012] Store Sale Integration from User-Provided PDF Flyer

**Status:** Backlog
**Priority:** Medium

> **Dependency on MEP-032:** When this story is implemented, the flyer ingest
> endpoint must consult `IClaudeAvailability.IsConfiguredAsync` before issuing
> any Claude Vision call. When the result is false, return a clear error
> linking to the AI section of the Settings page and do not persist any
> partial `StoreSale` rows (per MEP-032 AC scenario 11).

### Business Problem
Knowing which ingredients are on sale at my local grocery store would help me plan meals and shop around current deals. Earlier framing of this feature stalled because most major grocery chains prohibit scraping of their circular and pricing data. The revised approach is simpler and legally clean: I already download the weekly flyer as a PDF from the store's own website (they distribute it for consumer viewing), and the app parses *my own local copy* to extract sale items and match them to my `CanonicalIngredient` library. Factual pricing data (item name, sale price, unit pricing) is not copyrightable under 17 USC §102(b), so extracting facts from a legitimately-obtained PDF for personal single-user use is on firm ground. The legal-review blocker from the previous framing is removed.

Extraction uses Claude Vision (per-page structured output) rather than brittle PDF-text-extraction or scraping, because grocery flyers mix decorative layouts with prices expressed in idioms like "2 for $5" that regex-style parsers mishandle.

**Distribution caveat:** if this app is ever distributed beyond the single-user local deployment, the sale-parsing feature must be optional (disabled by default) so that the maintainer is not implicitly endorsing or facilitating any one grocer's data. Users who opt in take responsibility for supplying their own flyer PDFs obtained under each store's viewing terms.

### Acceptance Criteria
```gherkin
Feature: Store Sale Integration from User-Provided PDF Flyer

  Scenario: StoreSale entity persists extracted sale items with a validity window
    Given extracted items have a start and end date from the flyer
    When the schema is extended
    Then a StoreSale entity is added with columns: Id, CanonicalIngredientId, StoreName, SalePrice, RegularPrice, UnitPrice (nullable), ValidFrom, ValidTo, CreatedAt, SourceFlyerHash
    And an EF Core migration creates the table
    And an index on (CanonicalIngredientId, ValidFrom, ValidTo) supports "is this on sale right now" lookups

  Scenario: User provides a flyer PDF from a local path
    Given the user has downloaded a weekly flyer PDF from their grocer's website
    When the user invokes the sale-import action (CLI or API endpoint) with the PDF path and a store name
    Then the tool hashes the PDF content (for idempotency) and refuses to re-ingest a flyer already on record
    And the tool renders each PDF page to a PNG at a reasonable DPI (e.g. 200)

  Scenario: Claude Vision extracts structured sale data per page
    Given each PDF page has been rendered to PNG
    When the tool sends each image to the Claude API with a structured-output prompt
    Then Claude returns a JSON array of { ItemName, SalePrice, RegularPrice (nullable), UnitPrice (nullable), Size (nullable), Notes (nullable) }
    And the tool merges results across pages into a single item list per flyer
    And items with obviously-invalid extractions (e.g., SalePrice missing or non-numeric) are logged and skipped

  Scenario: Extracted items match to CanonicalIngredient rows
    Given the extracted item list
    When the matching pass runs
    Then each item is matched to an existing CanonicalIngredient via case-insensitive name lookup
    And unmatched items are written to a review queue (same pattern as MEP-026's UnresolvedUnitOfMeasureToken) so the user can confirm or create a new CanonicalIngredient
    And matched items become StoreSale rows scoped to the flyer's validity window

  Scenario: Shopping list surfaces currently-valid sales
    Given the user has a generated ShoppingList
    And one or more ShoppingListItem rows map to CanonicalIngredients that have an active StoreSale row
    When the user views the shopping list
    Then those items display a visible sale indicator with the sale price and validity end date
    And a filter lets the user view only items currently on sale

  Scenario: Sale-parsing is an opt-in feature gated by configuration
    Given the app may be distributed beyond a single-user local deployment
    When a user installs the app
    Then the store-sale ingest feature is disabled by default
    And enabling it requires an explicit UserPreferences opt-in with a short disclosure about the user's responsibility for obtaining flyers legitimately
    And the disclosure links to the store's publicly-posted terms where practical

  Scenario: No runtime dependency on any one grocer
    Given the design must not embed a specific grocer's assets
    When the feature is implemented
    Then no grocer logo, flyer URL, or proprietary data is bundled with the app
    And the StoreName field on StoreSale is free-text so any grocer's flyer can be ingested

  Scenario: Claude API unavailable disables the feature cleanly
    Given Claude Vision is the extraction engine
    And the user does not have a Claude API token configured (see MEP-032)
    When the user attempts to import a flyer
    Then a clear message explains that flyer parsing requires Claude and points at the Claude settings page
    And no partial StoreSale rows are persisted
```

---

## [MEP-013] Coupon Aggregation

**Status:** Post-MVP (Backlog)
**Priority:** Low

> **Scope note:** Post-MVP. Do not suggest or pick up until the initial release is shipped and a viable coupon-data source (third-party API partnership) is secured.

### Business Problem
Digital coupons from manufacturer and retailer programs could further reduce my grocery spending. Aggregating available coupons and matching them against my shopping list would highlight savings opportunities I might otherwise miss.

**Blocked Reason:** Same constraints as store sale integration. Coupon data requires third-party API partnerships that are not yet in place. Do not implement until a viable data source is secured.

### Acceptance Criteria
```gherkin
Feature: Coupon Aggregation

  Scenario: Match coupons to shopping list items
    Given I have a generated shopping list
    And digital coupon data is available
    When I view my shopping list
    Then items with matching coupons display the coupon value and redemption details

  Scenario: Show total potential savings
    Given my shopping list has items with matched coupons
    When I view the shopping list summary
    Then the total potential coupon savings is displayed
```

---

## [MEP-014] Canning Guidance

**Status:** Post-MVP (Backlog)
**Priority:** Low

> **Scope note:** Post-MVP. Do not suggest or pick up until the initial release is shipped.

### Business Problem
When seasonal produce is abundant and inexpensive, I may want to preserve it through canning. Guidance on preservation windows, estimated canning yields by produce type, and safety checklists would help me plan canning sessions confidently and safely. This feature would extend the seasonal produce guidance with actionable preservation information.

**Deferred Reason:** Post-MVP scope. A feature stub exists in the codebase at `Features/Canning/` but contains no implementation. Do not implement until explicitly instructed.

### Acceptance Criteria
```gherkin
Feature: Canning Guidance

  Scenario: View preservation window for seasonal produce
    Given "Tomatoes" are in season from July through September
    When I view canning guidance for "Tomatoes"
    Then the system shows the optimal preservation window
    And the window falls within or shortly after the peak season

  Scenario: Estimate canning yield
    Given I have 10 lb of "Tomatoes"
    When I request a canning yield estimate
    Then the system estimates the number of jars and jar size I can expect

  Scenario: Display safety checklist
    Given I am planning to can "Tomatoes"
    When I view the canning guidance
    Then a safety checklist specific to "Tomatoes" is displayed
    And the checklist covers acidity, processing time, and equipment requirements
```

---

## [MEP-015] Upcoming Expiration Dates

**Status:** Done
**Priority:** Medium

### Business Problem
I need clear visibility into which inventory items are approaching their expiry dates so I can plan my cooking and shopping accordingly. While MEP-009 (Waste Reduction Alerts) pairs expiring items with matching recipes, it does not provide a standalone view of all upcoming expirations. Without a dedicated expiration dashboard or view, I have to manually scan through my entire inventory across all three storage locations to identify what needs to be used soon. A sorted, at-a-glance view of upcoming expirations -- ordered by urgency -- lets me make informed decisions about what to cook, what to prioritize, and what to consume before it spoils.

### Acceptance Criteria
```gherkin
Feature: Upcoming Expiration Dates

  Scenario: View items approaching expiry sorted by urgency
    Given I have multiple inventory items with expiry dates set
    When I view the upcoming expiration dates
    Then items are listed in ascending order by expiry date
    And the soonest-expiring items appear first

  Scenario: Display days remaining until expiry
    Given I have an item "Greek Yogurt" expiring in 3 days
    When I view the upcoming expiration dates
    Then "Greek Yogurt" shows "3 days remaining"

  Scenario: Highlight items expiring within a critical window
    Given I have an item "Milk" expiring in 2 days
    And I have an item "Butter" expiring in 14 days
    When I view the upcoming expiration dates
    Then "Milk" is visually highlighted as critical (expiring within 3 days)
    And "Butter" is displayed without critical highlighting

  Scenario: Show items already past expiry
    Given I have an item "Sour Cream" with an expiry date of yesterday
    When I view the upcoming expiration dates
    Then "Sour Cream" appears at the top of the list
    And it is visually marked as expired

  Scenario: Exclude items without an expiry date
    Given I have an item "Salt" with no expiry date set
    When I view the upcoming expiration dates
    Then "Salt" does not appear in the list

  Scenario: Display storage location for each expiring item
    Given I have an item "Chicken Breast" in the Freezer expiring in 5 days
    When I view the upcoming expiration dates
    Then the entry for "Chicken Breast" shows its storage location as "Freezer"

  Scenario: Filter expiring items by storage location
    Given I have expiring items in Pantry, Fridge, and Freezer
    When I filter the upcoming expiration dates by location "Fridge"
    Then only items stored in the Fridge are displayed
```

---

## [MEP-016] Add Open Source Icons for Edit and Delete

**Status:** Done
**Priority:** Low

### Business Problem
The inventory table currently uses plain text or basic buttons for edit and delete actions. These controls are functional but do not provide the immediate visual recognition that icon-based controls offer. Replacing them with widely recognized open source icons (such as Material Icons pencil/edit and trash/delete) improves scannability and reduces the cognitive load of identifying available actions in the table. This is a small UI polish item that brings the interface closer to modern application conventions.

### Acceptance Criteria
```gherkin
Feature: Edit and Delete Icons in Inventory Table

  Scenario: Display edit icon on each inventory row
    Given I am viewing the inventory table
    When I look at any inventory item row
    Then the edit action is represented by a recognizable pencil/edit icon
    And the icon is sourced from an open source icon library

  Scenario: Display delete icon on each inventory row
    Given I am viewing the inventory table
    When I look at any inventory item row
    Then the delete action is represented by a recognizable trash/delete icon
    And the icon is sourced from an open source icon library

  Scenario: Edit icon triggers edit action
    Given I am viewing the inventory table
    When I click the edit icon on an inventory item row
    Then the edit dialog or form opens for that item

  Scenario: Delete icon triggers delete confirmation
    Given I am viewing the inventory table
    When I click the delete icon on an inventory item row
    Then a confirmation prompt is displayed before the item is removed

  Scenario: Icons are accessible
    Given I am viewing the inventory table
    When a screen reader reads an inventory item row
    Then the edit icon has an accessible label of "Edit"
    And the delete icon has an accessible label of "Delete"
```

---

## [MEP-017] Dark Mode Toggle

**Status:** Done
**Priority:** Low

### Business Problem
I often use this application in the evening when a bright white interface causes eye strain. A dark mode toggle would let me switch between light and dark themes based on my preference or ambient lighting conditions. This is a quality-of-life feature that makes the application more comfortable to use across different times of day and environments.

**Deferred Reason:** Post-MVP scope. No blocking dependency; this is a UI preference feature to be addressed after core functionality is complete.

### Acceptance Criteria
```gherkin
Feature: Dark Mode Toggle

  Scenario: Switch from light mode to dark mode
    Given the application is displaying in light mode
    When I toggle the theme to dark mode
    Then the application re-renders with a dark color scheme
    And text, icons, and controls remain legible against the dark background

  Scenario: Switch from dark mode to light mode
    Given the application is displaying in dark mode
    When I toggle the theme to light mode
    Then the application re-renders with the default light color scheme

  Scenario: Persist theme preference across sessions
    Given I have set my theme preference to dark mode
    When I close and reopen the application
    Then the application loads in dark mode

  Scenario: Default theme is light mode
    Given I have never set a theme preference
    When I open the application
    Then the application displays in light mode

  Scenario: Theme toggle is accessible from any screen
    Given I am on any screen in the application
    When I look for the theme toggle control
    Then the toggle is visible and reachable without navigating to a settings page
```

---

## [MEP-018] Recipe Detail and Manual Recipe Management

**Status:** Done
**Priority:** Medium

### Business Problem
When browsing my recipe library, I can see recipe titles but cannot view the full ingredient list without leaving the list view. I need a recipe detail view that shows all ingredients so I can quickly decide whether a recipe is practical for me right now. From that detail view, I also want to add all the recipe's ingredients to a shopping list in one action, eliminating the need to transcribe items manually. Additionally, imported recipes from TheMealDB should link back to their original source website so I can reference the author's notes, photos, or comments. Finally, not every recipe I want to cook comes from TheMealDB -- I need the ability to manually create my own recipes directly in the application, entering a title, ingredients with quantities and units, instructions, cuisine, and season affinity, so my full cooking repertoire is captured in one place.

### Acceptance Criteria
```gherkin
Feature: Recipe Detail and Manual Recipe Management

  Scenario: View recipe ingredients from the recipe list
    Given I am viewing my recipe library
    When I select a recipe "Spaghetti Aglio e Olio"
    Then a detail view opens showing the recipe title, instructions, cuisine, and dietary tags
    And the detail view lists all ingredients with their quantities and units of measure

  Scenario: View container reference notes on recipe ingredients
    Given a recipe ingredient was resolved from a container reference
    And the Notes field contains "1 can chopped tomatoes"
    When I view the recipe detail
    Then the ingredient row displays the resolved quantity and unit
    And the original container reference "1 can chopped tomatoes" is shown as a note

  Scenario: Add all recipe ingredients to a shopping list
    Given I am viewing the detail view of a recipe
    When I click "Add to Shopping List"
    Then all ingredients from the recipe are added to my shopping list
    And each shopping list item reflects the recipe's required quantity and unit

  Scenario: Shopping list aggregates quantities for duplicate ingredients
    Given my shopping list already contains "Olive Oil" at 30 ml
    And the recipe I am viewing requires 45 ml of "Olive Oil"
    When I click "Add to Shopping List"
    Then the shopping list entry for "Olive Oil" is updated to 75 ml
    And no duplicate entry is created

  Scenario: Link to original source website for imported recipes
    Given a recipe was imported from TheMealDB
    And the recipe has a source URL stored
    When I view the recipe detail
    Then a "View Original Source" link is displayed
    And clicking the link opens the source website in a new browser tab

  Scenario: No source link for manually created recipes
    Given a recipe was manually created by me
    When I view the recipe detail
    Then no "View Original Source" link is displayed

  Scenario: Manually create a new recipe
    Given I am on the recipe library screen
    When I click "Create Recipe"
    Then a form opens for entering a new recipe
    And the form includes fields for title, instructions, cuisine, and season affinity

  Scenario: Add ingredients to a manually created recipe
    Given I am creating a new recipe
    When I add an ingredient with name "Garlic", quantity 3, and unit "cloves"
    Then the ingredient appears in the recipe's ingredient list
    And I can add additional ingredients

  Scenario: Save a manually created recipe
    Given I have entered a title "Grandma's Tomato Soup"
    And I have added at least one ingredient
    And I have entered instructions
    When I save the recipe
    Then the recipe "Grandma's Tomato Soup" appears in my recipe library
    And the recipe is available for matching, meal planning, and dietary classification

  Scenario: Validate required fields on manual recipe creation
    Given I am creating a new recipe
    When I attempt to save without entering a title
    Then the system displays a validation error indicating the title is required
    And the recipe is not saved

  Scenario: Container reference detection on manual recipe entry
    Given I am creating a new recipe
    When I add an ingredient with a container reference such as "1 can" for "Diced Tomatoes"
    Then the system detects "can" as a container reference
    And the ingredient is flagged for user declaration of net weight or volume
```

---

## [MEP-019] Audit Code for Over-Complication

**Status:** Done
**Priority:** Medium

### Business Problem
The codebase has grown quickly with multiple agents contributing code across the API, frontend, and test projects. Without periodic review, unnecessary abstraction layers, dead code, unused imports, overly complex logic, and gold-plated features accumulate and make the system harder to understand, maintain, and debug. I need a thorough audit of all services, controllers, and components to simplify the codebase, remove anything that is not earning its keep, and ensure every public API surface is actually consumed. This is a code health item that reduces future maintenance burden and keeps the project approachable.

### Acceptance Criteria
```gherkin
Feature: Codebase Over-Complication Audit

  Scenario: Identify and remove dead code
    Given the full codebase has been reviewed
    When dead code is identified (methods, classes, or files that are never called or referenced)
    Then all dead code is removed
    And the solution still builds successfully
    And all existing tests still pass

  Scenario: Simplify overly abstract patterns
    Given a service, controller, or component uses abstraction layers that add indirection without clear benefit
    When the abstraction is reviewed
    Then the unnecessary abstraction is collapsed or inlined
    And the resulting code is functionally equivalent
    And all existing tests still pass

  Scenario: Reduce unnecessary indirection
    Given a code path passes through intermediate classes or methods that add no logic, transformation, or branching
    When the indirection is reviewed
    Then the pass-through layers are removed or consolidated
    And the calling code is updated to reference the simplified path
    And all existing tests still pass

  Scenario: Verify all public APIs are consumed
    Given all public methods and properties on services, controllers, and components have been inventoried
    When each public member is checked for at least one caller or consumer
    Then any public member with zero consumers is either removed or reduced to internal/private visibility
    And the solution still builds successfully

  Scenario: Remove unused imports and dependencies
    Given all source files have been reviewed
    When unused using directives, import statements, or package references are identified
    Then they are removed
    And the solution still builds successfully
    And all existing tests still pass

  Scenario: Confirm no gold-plating exists
    Given the implemented features have been compared against their backlog acceptance criteria
    When functionality beyond the stated acceptance criteria is identified
    Then that functionality is evaluated for removal or simplification
    And any removed functionality does not break existing acceptance criteria
```

---

## [MEP-020] Audit GitHub Workflows

**Status:** Done
**Priority:** Medium

### Follow-up correction
Two acceptance criteria were not actually satisfied when this was first marked
Done. Both were closed out later, after the gaps caused incidents on `main`:

- **"The directory paths in the Dependabot config match the actual project
  structure"** — the nuget entry watched `/src/MealsEnPlace.Api` only, leaving
  both tools projects and both test projects unwatched. GHSA-2m69-gcr7-jv3q
  (SQLitePCLRaw 2.1.11) therefore sat undetected in `tests/MealsEnPlace.Unit`
  until it failed the .NET Dependency Scan and was pinned by hand in #125. The
  config now watches all five project directories.
- **"All workflow jobs pass on a clean checkout"** — the auto-merge workflow
  gated on `fetch-metadata`'s `update-type`, which is inaccurate for grouped
  PRs. It reported `semver-patch` for the `@angular/material` + `@angular/cdk`
  21 -> 22 major in #75, which auto-merged into a repo whose `@angular/core`
  was still on 21 and broke `npm ci` on `main` for every branch until #124.
  Grouped PRs are no longer auto-merged, and the Angular release train is now
  a single Dependabot group.

### Business Problem
The project uses GitHub Actions for CI, CodeQL analysis, Dependabot, and auto-merge. As the project structure evolves -- new projects, changed dependencies, updated frameworks -- the workflow configurations can fall out of alignment with the actual codebase. Misconfigured triggers, stale cache keys, missing steps, redundant jobs, or incomplete Dependabot coverage lead to wasted CI minutes, false confidence in passing builds, or missed vulnerability scans. I need a thorough audit of all GitHub Actions workflows to ensure they are correct, efficient, and aligned with the current project structure.

### Acceptance Criteria
```gherkin
Feature: GitHub Workflow Audit

  Scenario: All workflow jobs pass on a clean checkout
    Given each GitHub Actions workflow file has been reviewed
    When every workflow is run against a clean checkout of the main branch
    Then all jobs complete successfully without errors or warnings

  Scenario: Cache keys match current project files
    Given workflows use caching for dependencies (NuGet, npm, etc.)
    When the cache key patterns are reviewed
    Then each cache key references the correct lock files or project files for the current project structure
    And no cache key references files that no longer exist

  Scenario: No unnecessary jobs run
    Given the workflow trigger conditions have been reviewed
    When a change is pushed that only affects frontend files
    Then backend-only jobs do not run (and vice versa)
    And no jobs run that produce no actionable output for the given change

  Scenario: Dependabot config covers all package ecosystems
    Given the project uses NuGet (.NET) and npm (Angular) package managers
    When the Dependabot configuration is reviewed
    Then both NuGet and npm ecosystems are configured for dependency updates
    And the directory paths in the Dependabot config match the actual project structure
    And GitHub Actions workflows are also covered for action version updates

  Scenario: CodeQL is configured for the correct languages
    Given the project contains C# and TypeScript source code
    When the CodeQL workflow configuration is reviewed
    Then CodeQL analysis is configured to scan both C# and TypeScript (or JavaScript)
    And no languages are listed that are not present in the project

  Scenario: Workflow steps are complete and correctly ordered
    Given each workflow has been reviewed step by step
    When the steps are evaluated against the project's pre-PR checklist and build requirements
    Then no required steps are missing (e.g., restore, build, test, format check)
    And steps are ordered so that dependencies are satisfied before dependent steps run
```

---

## [MEP-021] Progressive Web App (PWA)

**Status:** Done
**Priority:** Low

### Business Problem
I need to access Meals en Place from my phone and my wife's phone while in the kitchen or at the grocery store, without needing to carry a laptop or sit at a desktop. Currently the application is only usable through a desktop browser. Converting the Angular frontend into a Progressive Web App would let both of us install the app on our home screens (iOS and Android), load previously viewed data when connectivity is spotty in the store, and have a layout that works well on smaller screens. This is a post-MVP quality-of-life item that makes the application practical for its most common real-world usage scenarios.

### Acceptance Criteria
```gherkin
Feature: Progressive Web App (PWA)

  Scenario: Install on Android device
    Given I open the application in Chrome on an Android phone
    When the browser displays an "Add to Home Screen" prompt
    And I accept the prompt
    Then the application is installed on my Android home screen
    And launching it from the home screen opens the app in standalone mode without browser chrome

  Scenario: Install on iOS device
    Given I open the application in Safari on an iPhone
    When I use the Share menu and select "Add to Home Screen"
    Then the application is added to my iOS home screen with the configured icon
    And launching it from the home screen opens the app in standalone mode without browser chrome

  Scenario: Display home screen icon
    Given the application has been installed on a mobile device
    When I view my home screen
    Then the Meals en Place icon is displayed at the correct resolution for the device
    And the icon is not a generic browser favicon

  Scenario: Web app manifest is present and valid
    Given I navigate to the application URL
    When the browser reads the web app manifest
    Then the manifest includes a name, short_name, start_url, display mode set to "standalone", theme_color, background_color, and at least three icon sizes (192x192, 384x384, 512x512)

  Scenario: Service worker is registered
    Given I open the application in a supported browser
    When the page finishes loading
    Then a service worker is registered and active
    And the service worker caches the application shell (HTML, CSS, JS, fonts, icons)

  Scenario: Offline access to previously loaded data
    Given I have previously viewed my inventory list while online
    And the service worker has cached the response
    When I lose network connectivity
    And I open the application from the home screen
    Then the application shell loads without error
    And my most recently cached inventory data is displayed
    And a visible indicator informs me that I am viewing offline data

  Scenario: Offline access to previously loaded recipes
    Given I have previously viewed a recipe detail page while online
    And the service worker has cached the response
    When I lose network connectivity
    And I navigate to that recipe detail page
    Then the cached recipe detail is displayed including ingredients and instructions

  Scenario: Offline access to previously loaded shopping list
    Given I have previously viewed my shopping list while online
    And the service worker has cached the response
    When I lose network connectivity
    And I navigate to the shopping list
    Then the cached shopping list is displayed

  Scenario: Responsive layout on mobile screens
    Given I open the application on a phone with a viewport width of 375 pixels
    When I view the inventory list, recipe library, and shopping list screens
    Then all content is readable without horizontal scrolling
    And interactive controls (buttons, inputs, icons) are large enough to tap accurately
    And navigation is accessible without requiring a wide sidebar

  Scenario: Responsive layout on tablet screens
    Given I open the application on a tablet with a viewport width of 768 pixels
    When I view the inventory list, recipe library, and shopping list screens
    Then the layout adapts to use available space without excessive whitespace
    And all features remain fully functional

  Scenario: Push notification readiness
    Given the service worker is registered
    When I check the service worker capabilities
    Then the service worker includes a push event listener stub
    And the application requests notification permission from the user
    And granting permission registers the subscription with the backend
    And no push notifications are sent until a future feature (such as waste alerts) activates them
```

---

## [MEP-022] User-Controlled Display Unit for Inventory Items

**Status:** Done
**Priority:** Medium

### Business Problem
When I add an inventory item in a specific unit -- for example, "32 fl oz" of chicken broth -- the system converts it to metric base units internally and then applies automatic threshold-based display conversion rules that may show it back to me in a completely different unit, such as "1 qt." This is confusing because the quantity on my shelf says "32 fl oz" and I expect to see that same unit in the application. I have no way to control which display unit is used; the system decides for me based on quantity ranges. I need the application to remember the unit I entered and display the item in that unit by default. When I want to see the quantity in a different compatible unit (for example, converting 32 fl oz to quarts for easier mental math), I should be able to choose that conversion explicitly, and the application should then display the item in my chosen unit going forward.

### Acceptance Criteria
```gherkin
Feature: User-Controlled Display Unit for Inventory Items

  Scenario: Display item in the unit it was entered in
    Given I add an inventory item "Chicken Broth" with quantity 32 and unit "fl oz"
    When I view the inventory list
    Then "Chicken Broth" displays as "32 fl oz"
    And the display does not automatically convert to a different unit such as "1 qt"

  Scenario: Display item in the entry unit after editing quantity
    Given I have an inventory item "Olive Oil" entered with unit "ml" and quantity 500
    When I edit the quantity to 750
    Then "Olive Oil" displays as "750 ml"
    And the display unit remains "ml"

  Scenario: Convert display unit to a compatible unit
    Given I have an inventory item "Chicken Broth" displaying as "32 fl oz"
    When I choose to convert the display unit to "qt"
    Then "Chicken Broth" displays as "1 qt"
    And the internal stored quantity remains unchanged in metric base units

  Scenario: Persist the user-chosen display unit across sessions
    Given I have converted "Chicken Broth" to display as "1 qt"
    When I close and reopen the application
    Then "Chicken Broth" still displays as "1 qt"

  Scenario: Only compatible units are offered for conversion
    Given I have an inventory item "Flour" entered with unit "lb"
    When I open the display unit conversion options
    Then the options include weight-compatible units such as "oz", "g", and "kg"
    And the options do not include volume units such as "fl oz", "cup", or "ml"

  Scenario: New items default to their entry unit
    Given I add an inventory item "Soy Sauce" with quantity 15 and unit "fl oz"
    And I do not choose a different display unit
    When I view the inventory list
    Then "Soy Sauce" displays as "15 fl oz"

  Scenario: Metric entry unit is preserved for metric users
    Given I add an inventory item "Butter" with quantity 250 and unit "g"
    When I view the inventory list
    Then "Butter" displays as "250 g"
    And the display does not automatically convert to "oz" or "lb"
```

---

## [MEP-023] Input Sanitization Audit

**Status:** Done
**Priority:** High

### Business Problem
The application accepts user-entered text in multiple places — inventory item notes, recipe titles, recipe instructions, ingredient names, meal plan names, and manual recipe entries. If any of this data is logged, stored, or rendered without sanitization, it creates risk for log injection, stored XSS (if rendered as HTML), or database issues from malformed input. I need a thorough audit of every code path that handles user-entered strings, creation of a shared sanitization utility, and updating all input-handling code to use it. The audit should cover both the API (C#) and frontend (Angular/TypeScript) layers.

### Acceptance Criteria
```gherkin
Feature: Input Sanitization Audit

  Scenario: Identify all user input entry points
    Given the full codebase has been reviewed
    When all API endpoints that accept string input from users are inventoried
    Then each entry point is documented with the fields it accepts

  Scenario: Create a shared sanitization utility
    Given the audit has identified all user input entry points
    When a sanitization utility is created
    Then it provides methods for HTML encoding, trimming, and null-safe string cleaning
    And it is usable from both services and controllers

  Scenario: Sanitize user input before storage
    Given a user submits a string containing HTML tags or script content
    When the input is processed by the API
    Then the stored value has dangerous content neutralized
    And the original meaning of the text is preserved

  Scenario: Sanitize user input before logging
    Given a user submits a string containing newline characters or log-injection patterns
    When the input is logged
    Then the logged value has control characters removed or escaped
    And the log entry cannot be used to forge additional log lines

  Scenario: Sanitize user input before display
    Given a user has stored a string containing HTML entities or script tags
    When the value is rendered in the Angular frontend
    Then the value is safely escaped by Angular's built-in XSS protection
    And no raw HTML is rendered from user-supplied data

  Scenario: All existing user input paths use the sanitization utility
    Given the sanitization utility has been created
    When all user input entry points are reviewed
    Then every endpoint that stores or logs user-entered strings calls the sanitization utility
    And the solution builds and all tests pass

  Scenario: No unsanitized user data in log output
    Given the full codebase has been reviewed for logging calls
    When any log statement includes user-provided data
    Then the data is passed through the sanitization utility before logging
    And no raw user input appears in log output
```

---

## [MEP-024] PlantUML C4 Diagram PNG Generation via GitHub Actions

**Status:** Done
**Priority:** Low

> **Superseded in part (2026-09-07).** The GitHub Actions workflow was removed and
> rendering moved to `scripts/render-c4.sh`, run locally as pre-PR checklist step 3.
>
> The workflow committed rendered PNGs directly to `main`. When the `main` ruleset was
> tightened on 2026-08-16 to require pull requests, those pushes started being rejected
> with `GH013: Repository rule violations found`. The job failed on every merge from
> then on and the committed diagrams went stale for three merges without anyone
> noticing, because the failure was in a workflow nobody was watching.
>
> GitHub Actions cannot be added to a ruleset bypass list — the eligible actors are
> repository admins, write-role holders, teams, GitHub Apps, and Dependabot — so
> keeping the workflow would have required introducing a PAT, a GitHub App, or a
> deploy key purely to render diagrams.
>
> This reverses this item's "without requiring any local tooling" premise: rendering
> now needs Docker, which the project already requires for PostgreSQL. The rest of the
> item stands — PNGs are still committed beside their sources and still embedded in
> the README, so nothing changes for anyone reading the repo on GitHub.

### Business Problem
The project maintains PlantUML C4 architecture diagrams in docs/c4/ (context.puml, container.puml, component-api.puml, component-web.puml), but viewing them requires a local PlantUML installation or a compatible IDE plugin. This creates friction for anyone reviewing the repository on GitHub, where .puml files render as plain text. I need an automated GitHub Actions workflow that renders these diagrams to PNG whenever they change, so that up-to-date rendered diagrams are always available without requiring any local tooling. The README can then embed the PNG files directly for inline viewing.

### Acceptance Criteria
```gherkin
Feature: PlantUML C4 Diagram PNG Generation

  Scenario: Workflow triggers on .puml file changes
    Given the GitHub Actions workflow for PlantUML rendering is configured
    When a commit is pushed that modifies any file matching docs/c4/*.puml
    Then the PlantUML rendering workflow is triggered
    And the workflow does not trigger for changes outside docs/c4/*.puml

  Scenario: PNGs are generated for all C4 diagrams
    Given the PlantUML rendering workflow has been triggered
    When the workflow executes the PlantUML renderer
    Then a PNG file is produced for each .puml file in docs/c4/
    And the PNG files are named to match their source files (e.g., context.puml produces context.png)

  Scenario: Generated PNGs are committed to the repository
    Given the workflow has produced PNG files for all diagrams
    When the rendering step completes successfully
    Then the PNG files are committed to docs/c4/ alongside the .puml sources
    And the commit message clearly indicates it is an automated diagram render
    And no workflow loop is created by the automated commit

  Scenario: Generated PNGs are available as workflow artifacts
    Given the workflow has produced PNG files for all diagrams
    When the rendering step completes successfully
    Then the PNG files are also uploaded as downloadable workflow artifacts
    And the artifacts are retained for at least 7 days

  Scenario: README references rendered PNG diagrams
    Given PNG files exist in docs/c4/ for all C4 diagrams
    When the project README is updated
    Then the README embeds or links to the PNG files in docs/c4/
    And the diagrams are visible inline when viewing the README on GitHub

  Scenario: Diagrams render correctly using C4-PlantUML stdlib
    Given the .puml files include C4-PlantUML macros from the plantuml-stdlib GitHub repository
    When the PlantUML renderer processes the files
    Then the renderer resolves the remote C4-PlantUML includes successfully
    And the rendered PNGs accurately reflect the C4 diagram content
```

## [MEP-025] Spike: Evaluate Expanded Recipe Data Sources

**Status:** Done
**Priority:** Medium

**Recommendation:** adopt the Kaggle "Recipe Dataset (over 2M)" with `source != 'Recipes1M'` filter. Implementation details under MEP-026. Full spike output in [docs/spikes/mep-025-recommendation.md](spikes/mep-025-recommendation.md).

### Business Problem
TheMealDB, the current recipe source, carries approximately 600 meals. This catalog is functional but limiting -- it restricts recipe matching variety, meal plan diversity, and the overall usefulness of the "What can I make?" feature as my ingredient inventory grows. Before committing to a specific integration, I need a time-boxed spike to evaluate alternative recipe data sources across two categories: static datasets suitable for one-time bulk import (preferred for a single-user local deployment that should not burn API quota per query) and live APIs (as a fallback if no static dataset meets quality requirements). The spike should produce a recommendation with concrete data on import viability, ingredient matching quality, licensing constraints, and storage impact.

**Leading candidate:** [wilmerarltstrmberg/recipe-dataset-over-2m on Kaggle](https://www.kaggle.com/datasets/wilmerarltstrmberg/recipe-dataset-over-2m). 2.23M rows aggregated from 28 recipe sites; filter `source != 'Recipes1M'` on ingest leaves ~1.64M usable rows (still ~2,700× TheMealDB's catalog). License is CC BY-NC-SA 4.0, compatible with single-user personal use. The dataset's NER column provides pre-extracted canonical ingredient names per recipe, which maps directly into `CanonicalIngredient` and sizeably reduces seed-curation work. Detailed analysis in [docs/spikes/mep-025-kaggle-2m-findings.md](spikes/mep-025-kaggle-2m-findings.md).

**Out of scope for MVP:** live-API candidates (Spoonacular, Edamam). Per-query quota burn is architecturally incompatible with a single-user local deployment. Revisit only if the static-dataset path proves unworkable.

**Rejected candidates:**

- **Recipe1M+ (MIT CSAIL)** -- initial leading candidate based on its layer2+ structured ingredient data. Rejected after a dataset access request to MIT was returned with revised terms restricting access to universities and public institutions only. This project is a single-user personal tool without institutional affiliation, so Recipe1M+ is unavailable regardless of the underlying data fit. The citation references and discovery path are preserved in [docs/spikes/mep-025-recipe1m-references.md](spikes/mep-025-recipe1m-references.md) in case a future reader at an eligible institution wants to follow the same trail.
- **RecipeNLG** -- derived from Recipe1M+. The chosen Kaggle dataset is effectively a RecipeNLG reupload; filtering `source != 'Recipes1M'` at ingest sidesteps the Recipe1M+ dependency cleanly.

### Acceptance Criteria
```gherkin
Feature: Evaluate Expanded Recipe Data Sources

  Scenario: Evaluate static dataset candidates
    Given the following static datasets are under consideration:
      | Dataset       | Approximate Size | License      |
      | Recipe1M+     | ~1,000,000       | MIT          |
      | RecipeNLG     | ~2,000,000       | Research     |
      | Food.com (Kaggle) | ~230,000     | CC BY-NC-SA  |
    When each dataset is reviewed
    Then the evaluation documents format compatibility with the existing recipe import pipeline
    And the evaluation documents licensing terms and whether redistribution or local use is permitted

  Scenario: Evaluate live API candidates
    Given the following live APIs are under consideration:
      | API           | Approximate Catalog | Free Tier Limits     |
      | Spoonacular   | ~400,000            | 150 requests/day     |
      | Edamam        | ~2,000,000          | 5-10 requests/minute |
    When each API is reviewed
    Then the evaluation documents rate limits and whether a one-time bulk fetch is feasible within the free tier
    And the evaluation documents data format compatibility with the existing recipe import pipeline

  Scenario: Measure import success rate against the existing pipeline
    Given a sample of at least 500 recipes from each candidate source has been obtained
    When each sample is run through the existing recipe import and parsing pipeline
    Then the percentage of recipes that parse cleanly without manual intervention is recorded
    And any systematic parsing failures are categorized

  Scenario: Measure match quality against the CanonicalIngredient table
    Given the sample recipes have been parsed
    When each recipe's ingredients are matched against the existing CanonicalIngredient table
    Then the percentage of ingredients that match an existing canonical entry is recorded
    And the number of new canonical entries that would need to be created is documented

  Scenario: Measure container-reference flag rate
    Given the sample recipes have been parsed
    When each recipe's ingredients are checked for container references
    Then the percentage of recipes that land in "Awaiting Resolution" status is recorded
    And the total number of unresolved container references across the sample is documented

  Scenario: Assess storage impact on PostgreSQL
    Given the full dataset size for each candidate is known
    When the estimated row count and storage footprint are calculated for recipes, recipe ingredients, and canonical ingredients
    Then the projected database size increase is documented for each candidate
    And any concerns about query performance at the projected scale are noted

  Scenario: Verify licensing permits local single-user use
    Given each candidate's license terms have been reviewed
    When the license is evaluated against the project's use case (local, single-user, non-commercial)
    Then sources with licenses that prohibit local use or require attribution not feasible in-app are flagged
    And the recommendation clearly states which sources are safe to use

  Scenario: Sanitize narrative prose from imported instructions
    Given a sample batch from the chosen dataset has been parsed
    When each recipe's instruction steps are run through a prose-stripping filter
    Then sentences containing first-person pronouns, long parentheticals, or non-imperative structure are removed
    And recipes retaining fewer than 80% of their original steps are flagged for review or exclusion
    And the pass rate across the sample is recorded

  Scenario: Dataset is obtained per-user, not redistributed
    Given the chosen dataset's license restricts redistribution (CC BY-NC-SA, research-only, or similar non-commercial terms)
    When setup documentation is written
    Then the docs link to the upstream source (Kaggle dataset page, MIT project page, or equivalent) as the canonical entry point, from which users can locate the license and download instructions themselves
    And the docs do NOT bypass any upstream signup or agreement step
    And no source data (recipe JSON, SQL dumps, seed fixtures containing real recipe text) is committed to the repository
    And any seed data derived from the dataset is limited to non-copyrightable elements only (canonical ingredient names, UOM mappings)

  Scenario: Produce a recommendation
    Given all evaluation criteria have been assessed for every candidate
    When the spike is complete
    Then a written recommendation identifies the preferred data source (or combination)
    And the recommendation justifies the choice based on import success rate, match quality, container-reference rate, licensing, and storage impact
    And the recommendation includes a proposed approach for integration (bulk import vs. incremental sync)
```

## [MEP-026] Bulk Recipe Ingest from Kaggle 2M Dataset with UOM Alias Table

**Status:** Done
**Priority:** Medium

### Business Problem
MEP-025 selected the Kaggle "Recipe Dataset (over 2M)" as the recipe catalog source, with the `source != 'Recipes1M'` subset providing ~1.64M usable recipes -- roughly 2,700x the current TheMealDB catalog. The spike also surfaced three concrete pipeline gaps that must be closed before the data is usable: the existing UOM parser misses the dotted-abbreviation style common in the dataset (`c.`, `tsp.`, `oz.`, `Tbsp.`), count-with-ingredient-noun patterns (`"4 chicken breasts"`) fall through to Claude unnecessarily, and the prototype prose filter over-drops legitimate imperatives that start with a preposition. This story implements the dataset ingest as an offline admin tool, adds a UOM alias table with a human-in-the-loop review queue (mirroring the MEP-003 container-resolution pattern) to reduce Claude invocations and keep the user in control, and closes the identified parser gaps. Full design rationale and measurement results in [docs/spikes/mep-025-recommendation.md](spikes/mep-025-recommendation.md).

### Acceptance Criteria
```gherkin
Feature: Bulk Recipe Ingest from Kaggle 2M Dataset with UOM Alias Table

  Scenario: UnitOfMeasureAlias entity is added to the schema
    Given the UOM model currently supports abbreviation and name lookups
    When a new UnitOfMeasureAlias entity is introduced
    Then the entity has columns for alias text (case-insensitive), target UnitOfMeasure foreign key, and creation timestamp
    And the alias text column is indexed for efficient lookup
    And an EF Core migration creates the table without modifying existing UnitOfMeasure rows

  Scenario: UOM alias table is seeded with common variants
    Given the UnitOfMeasureAlias entity exists
    When the migration seeds common alias variants
    Then the following mappings are present: c/c. -> cup, t/t. -> teaspoon, T/T./Tbs/Tbsp./Tbl -> tablespoon, tsp. -> teaspoon, oz./ozs/ozs. -> ounce, lb./lbs/lbs. -> pound, fl. oz/fluid oz/fl. ozs -> fluid ounce, ml./mls -> milliliter, g./gm/gms -> gram, kg./kgs -> kilogram, pt./pts -> pint, qt./qts -> quart
    And each alias row maps to an existing UnitOfMeasure via foreign key

  Scenario: UomNormalizationService consults the alias table before falling back to Claude
    Given a measure string with a recognized alias (e.g. "1 c. flour")
    When UomNormalizationService.NormalizeAsync is called
    Then the service resolves the unit deterministically via the alias table
    And the returned NormalizationResult has Confidence = High and WasClaudeResolved = false
    And Claude is not invoked for any alias-matched token

  Scenario: Unresolved UOM tokens are queued for user review
    Given a measure string with a unit token that matches no abbreviation, name, or alias
    When UomNormalizationService.NormalizeAsync is called in ingest mode
    Then an UnresolvedUnitOfMeasureToken row is written capturing the original measure string, the extracted unit token, and the ingredient context
    And the ingestion of that ingredient is deferred until the token is resolved
    And Claude is NOT automatically invoked for unresolved tokens during bulk ingest

  Scenario: User resolves an unresolved token via the review queue
    Given one or more UnresolvedUnitOfMeasureToken rows exist
    When the user reviews a token via a UI or CLI
    Then the user may choose: (a) map to an existing UnitOfMeasure (creates a new UnitOfMeasureAlias row), (b) defer to Claude for this one occurrence, or (c) ignore this token permanently
    And choosing (a) retroactively resolves every deferred ingredient that matched the same unresolved token
    And the UnresolvedUnitOfMeasureToken row is deleted after the decision is persisted

  Scenario: Alias uniqueness is enforced by the service, not the database
    Given recipe notation uses case meaningfully (uppercase "T" = Tablespoon, lowercase "t" = Teaspoon, a 3x quantity difference)
    And the database has no unique index on UnitOfMeasureAlias.Alias
    When the user attempts to create an alias that already exists (case-sensitive match)
    Then the service rejects the duplicate by default with a clear error
    And the user may re-submit with an explicit override flag to force the insert
    And the override path is required only for legitimate case-sensitive variants (e.g. "T" and "t", "Tbsp." and "tbsp.")

  Scenario: Count-with-ingredient-noun defaults to "ea"
    Given a measure string with a positive numeric quantity and no matching unit, alias, or container keyword (e.g. "4 chicken breasts")
    When UomNormalizationService.NormalizeAsync is called
    Then the service resolves to the "ea" UnitOfMeasure with the parsed quantity
    And the returned NormalizationResult has WasClaudeResolved = false
    And Confidence = High

  Scenario: Prose filter retains legitimate imperatives
    Given a recipe instruction step that starts with a preposition or subordinator (e.g. "In a bowl, combine..." or "When the mixture bubbles, stir...")
    When the prose filter runs during ingest
    Then the step is retained if it contains no first-person pronouns and is <= 40 words
    And the step is NOT dropped solely because its first word is not an imperative verb

  Scenario: NER column seeds CanonicalIngredient rows in bulk
    Given a parsed recipe from the Kaggle 2M dataset with a populated NER array
    When the ingest tool processes the recipe
    Then each unique NER token creates a CanonicalIngredient row if one does not already exist (case-insensitive match)
    And duplicates across recipes are deduplicated
    And the resulting CanonicalIngredient count after a full ingest run is recorded and bounded (projection: 5,000 to 15,000 rows)

  Scenario: Ingest runs as an offline admin tool
    Given the user has downloaded the Kaggle dataset CSV to their local machine
    When the user runs the ingest tool (e.g. MealsEnPlace.Tools.Ingest <csv-path>)
    Then the tool filters rows where source = 'Recipes1M' and ignores them
    And the tool streams the CSV without loading the full 2.31 GB into memory
    And the tool reports a final summary including: total recipes ingested, container-flagged ingredient count, UOM tokens sent to the review queue, canonical ingredients created, and elapsed time
    And no recipe data from the dataset is exposed via any runtime API endpoint or committed to the repository

  Scenario: Setup documentation points users at Kaggle
    Given a fresh clone of the repository
    When a user reads the setup documentation
    Then the docs link to the Kaggle dataset page as the canonical source
    And the docs describe the required user action (Kaggle account, dataset download, placing the CSV at a local path)
    And the docs do NOT bundle, mirror, or commit any dataset content
    And a CITATION.cff at the repository root credits the dataset source per CC BY-NC-SA 4.0 attribution

  Scenario: Container-resolution flow handles high-volume dataset input
    Given an ingest run has produced a significant number of container-flagged RecipeIngredient rows (projection: ~45% of recipes, ~750k recipes flagged for the full dataset)
    When the user opens the container-resolution UI
    Then the UI surfaces flagged ingredients grouped by canonical ingredient so the user can resolve "1 can diced tomatoes" once and apply the decision to every occurrence
    And progress is persisted so the user can resolve in sessions rather than all at once

  Scenario: License constraints are honored in the implementation
    Given CC BY-NC-SA 4.0 terms apply to the dataset
    When the implementation is reviewed
    Then no recipe JSON, SQL dump, or fixture containing real recipe text is present in the repository
    And no test uses real dataset text as input (tests use synthetic fixtures)
    And the application is not deployed beyond the user's local machine
    And any future commercialization triggers a re-evaluation of the data source
```

## [MEP-027] Mark Meal as Eaten with Optional Inventory Auto-Deplete

**Status:** Done
**Priority:** Medium

### Implementation Notes
Shipped alongside MEP-031 on branch `feature/mep-027-mep-031-meal-consumption-with-auto-deplete`. Scope covered:

- Schema: `MealPlanSlot` gained nullable `ConsumedAt` and `ConsumedWithAutoDeplete`; `UserPreferences` gained `AutoDepleteOnConsume` (default false). New `ConsumeAuditEntry` table with one row per `InventoryItem` decrement captures the restore trail (`OriginalInventoryItemId`, `DeductedQuantity`, `OriginalLocation`, `OriginalExpiryDate`). Migration `20260419235648_AddMealConsumptionAndAutoDepleteAudit` smoke-tested Up + Down + Up with no data loss.
- `MealConsumptionService` owns the consume pipeline: marks the slot, captures the current preference, and (when auto-deplete is on) deducts each recipe ingredient from inventory oldest-expiry-first (null expiry last). Cross-type rows (Volume vs Weight) are skipped rather than guessed. Short ingredients clamp inventory to 0 and surface via `ShortIngredient` entries; the consume still succeeds.
- Endpoints: `POST /api/v1/meal-plan-slots/{id}/consume` returns `ConsumeMealResponse { consumedAt, autoDepleteApplied, shortIngredients[] }`. `UserPreferencesController` PUT accepts an optional `autoDepleteOnConsume` (omitted = leave alone).
- Angular: meal plan board slot cards gained a "Mark eaten" / "Unmark" action, a green checkmark + muted styling on consumed slots, and a snackbar that lists any short ingredients. Settings page Inventory Behavior section now hosts the `AutoDepleteOnConsume` toggle wired through `PreferencesService`.

### Business Problem
When a planned meal is cooked, the ingredients it used are no longer in inventory but the system still thinks they are. Manually opening each ingredient and subtracting the amount consumed is friction I would rather not accept. I want to mark a `MealPlanSlot` as "eaten" from the meal plan board, and -- if I opt in -- have the system automatically deduct the recipe's ingredient quantities from current inventory in the background. The opt-in toggle matters because some users (or some weeks) want a review-before-commit experience, while others want friction-free auto-deplete. The default is off so inventory is never silently modified without explicit consent.

### Acceptance Criteria
```gherkin
Feature: Mark Meal as Eaten with Optional Inventory Auto-Deplete

  Scenario: MealPlanSlot gains a Consumed state
    Given the existing MealPlanSlot entity
    When the schema is extended
    Then a ConsumedAt nullable DateTime column is added to MealPlanSlot
    And a ConsumedWithAutoDeplete nullable boolean column is added to record whether the user's preference was on at the time of consume
    And an EF Core migration applies these columns without data loss

  Scenario: User marks a slot as eaten from the meal plan board
    Given a MealPlanSlot with an assigned Recipe
    When the user clicks "Mark as eaten" on that slot
    Then ConsumedAt is set to the current UTC time
    And the UI renders the slot with a visual indicator (checkmark, muted styling, or similar)
    And a POST /api/v1/meal-plan-slots/{id}/consume endpoint persists the state

  Scenario: UserPreferences gains the AutoDepleteOnConsume toggle
    Given the existing UserPreferences singleton
    When the schema is extended
    Then an AutoDepleteOnConsume boolean column is added with default false
    And a settings UI control exposes the toggle with a clear description of what it does

  Scenario: Consuming a slot with auto-deplete ON deducts ingredients from inventory
    Given AutoDepleteOnConsume is true
    And the Recipe has RecipeIngredient rows each mapped to a CanonicalIngredient
    When the user marks the slot as eaten
    Then for each RecipeIngredient the service selects matching InventoryItem rows for that CanonicalIngredient
    And decrements Quantity from the oldest-expiry row first, falling back to the next row when one is exhausted
    And the ConsumedWithAutoDeplete column on the slot is set to true

  Scenario: Consuming a slot with auto-deplete OFF is a state-only change
    Given AutoDepleteOnConsume is false
    When the user marks the slot as eaten
    Then ConsumedAt is set but no InventoryItem rows are modified
    And the ConsumedWithAutoDeplete column on the slot is set to false

  Scenario: Insufficient inventory surfaces a warning but does not block the consume
    Given auto-deplete is on
    And the Recipe calls for 500g of flour
    And total flour in inventory is only 300g across all rows
    When the user marks the slot as eaten
    Then inventory flour is depleted to 0g (not below)
    And the UI shows a warning listing the short ingredients
    And the consume still succeeds
```

## [MEP-028] Push Shopping List to External Todo Provider (Todoist first)

**Status:** Done
**Priority:** Medium

### Implementation Notes
Shipped alongside MEP-029 on branch `feature/mep-028-mep-029-todoist-push`. Scope covered:

- `IShoppingListPushTarget` abstraction with `TodoistShoppingListPushTarget` as the first implementation. The controller + settings flow are provider-agnostic; additional providers can slot in without touching callers.
- New endpoints: `POST /api/v1/meal-plans/{mealPlanId}/shopping-list/push/todoist` and `POST /api/v1/shopping-list/push/todoist` (standalone list). Both return `ShoppingListPushResult { created, updated, closed, unchanged }`.
- Infrastructure: `Infrastructure/ExternalApis/Todoist/` houses `TodoistClient` (REST v2: create/update/close tasks) and `TodoistOptions` bound from `IConfiguration` under the `Todoist` section.
- Shared `ExternalTaskLink` table drives idempotency — one row per pushed item keyed by `(SourceType, SourceId, Provider)`, with `SourceScope` recording the meal-plan id (or `"standalone"`) so removed-item detection works without joining the (possibly deleted) source. `ContentHash` is a SHA-256 of the task title; re-push recomputes and PATCHes only when the hash differs. Removed items close the remote task (not delete) so Todoist's completed-history is preserved.
- Angular: "Push to Todoist" button on the shopping list page, gated by `TodoistAvailabilityService` (polls `GET /api/v1/settings/todoist/status` on app init). Success snackbar reports counts; errors surface the server's `ProblemDetails.Detail`.
- `GET /api/v1/settings/todoist/status` endpoint reflects whether `Todoist:Token` is populated.

### Deferred (scope decisions)
- **Settings-page token entry + test-connection + project picker → MEP-035.** This PR reads the token from `dotnet user-secrets` as `Todoist:Token` to match the user's existing workflow. MEP-035 lands the encrypted-at-rest DataProtection flow and the `GET /projects` dropdown described in the original AC.
- **Associated project quick-pick → MEP-036.** The `ExternalProjectId` column on `ExternalTaskLink` is populated on every push so MEP-036 can enumerate previously-used projects without an extra Todoist round-trip.

### Business Problem
My shopping list currently lives only in the app. When I am at the grocery store on my phone, I would rather glance at Todoist -- which I already use for life errands -- than open a separate web app. I want a one-click action that pushes the current shopping list to my Todoist account, with each shopping item becoming a Todoist task under a configurable project. Todoist is the first target because it has a clean documented REST API and apps on every platform; the design should introduce an abstraction boundary so Google Tasks, Microsoft To Do, or Apple Reminders can slot in later without a rewrite.

### Acceptance Criteria
```gherkin
Feature: Push Shopping List to External Todo Provider

  Scenario: IShoppingListPushTarget abstraction defines the contract
    Given multiple todo providers may be supported over time
    When the feature is designed
    Then an IShoppingListPushTarget interface is introduced with a PushAsync method
    And a TodoistShoppingListPushTarget implementation is registered as the first provider
    And the abstraction allows additional providers to be added without touching callers

  Scenario: User configures a Todoist API token
    Given Todoist requires a personal API token for authentication
    When the user opens the provider settings
    Then a password-style input accepts the Todoist API token
    And the token is stored via dotnet user-secrets locally (never in the database or repo)
    And a "Test connection" button verifies the token by hitting the Todoist /projects endpoint

  Scenario: User configures a target Todoist project
    Given the user may want shopping items in a specific project (not Inbox)
    When configuration is open
    Then the UI fetches the user's Todoist projects and presents a dropdown
    And the selected project ID is saved to UserPreferences (or equivalent)
    And a missing / deleted project falls back to Inbox with a clear message

  Scenario: User pushes a shopping list to Todoist
    Given a ShoppingList exists with one or more ShoppingListItem rows
    And a valid Todoist token and project are configured
    When the user clicks "Push to Todoist" on the shopping list page
    Then each ShoppingListItem becomes a Todoist task titled "{Quantity} {UomAbbreviation} {IngredientName}"
    And the tasks are created in the configured Todoist project
    And a last-pushed timestamp is persisted per ShoppingList

  Scenario: Re-pushing an already-pushed list updates rather than duplicates
    Given a ShoppingList was previously pushed to Todoist
    And the ShoppingList items have changed (added, removed, or quantities changed)
    When the user pushes again
    Then the existing Todoist tasks are updated / closed / created as needed
    And no duplicate tasks are created for items that already exist

  Scenario: Network or Todoist errors are surfaced as retryable
    Given the Todoist API is unreachable or returns an error
    When the user pushes
    Then a clear error message is shown with the Todoist-reported reason
    And the shopping list remains in a push-eligible state so the user can retry
    And no partial push leaves the user uncertain about what succeeded
```

## [MEP-029] Push Meal Plan to External Todo Provider (Todoist first)

**Status:** Done
**Priority:** Medium

### Implementation Notes
Shipped alongside MEP-028 on the same branch because both consume the shared `ExternalTaskLink` idempotency table and the shared `TodoistClient`.

- `IMealPlanPushTarget` / `TodoistMealPlanPushTarget` mirror the shopping list push contract. Each `MealPlanSlot` becomes one Todoist task titled `"{MealSlot}: {RecipeTitle}"` (e.g., `"Dinner: Chicken Scampi"`).
- `due_date` is computed from `MealPlan.WeekStartDate` + the slot's `DayOfWeek` offset using the same Monday-first rotation as `MealPlanService` generation.
- `POST /api/v1/meal-plans/{id}/push/todoist` endpoint. Returns `MealPlanPushResult { created, updated, closed, unchanged }`; 400 when Todoist isn't configured, 404 when the plan doesn't exist.
- Hash input combines the content string and the due date so a recipe swap *or* a day shuffle trips the re-push-as-update branch.
- Angular: "Push to Todoist" action on the meal plan board header, gated by the same `TodoistAvailabilityService` signal used by MEP-028. Success snackbar reports counts.

### Shared pieces with MEP-028
- Shared `ExternalTaskLink` schema + migration `20260420012850_AddExternalTaskLink` (smoke-tested Up/Down/Up against local Postgres).
- Shared `TodoistClient` + `TodoistOptions` binding.
- Shared `TodoistAvailabilityService` signal + `GET /api/v1/settings/todoist/status` endpoint.

### Deferred (scope decisions)
- Token settings UI → **MEP-035** (reads from `Todoist:Token` user secret for now).
- Project quick-pick → **MEP-036** (`ExternalProjectId` column on `ExternalTaskLink` is populated on every push so the history is there when MEP-036 queries it).

### Business Problem
Sister story to MEP-028. I want my weekly meal plan visible in Todoist alongside the rest of my calendar and tasks, so I can see "what's for dinner Thursday" at a glance without opening the Meals en Place app. Each `MealPlanSlot` should become a Todoist task scheduled for the slot's date, with a title like `"Dinner: Chicken Scampi"`. The same provider abstraction from MEP-028 applies.

### Acceptance Criteria
```gherkin
Feature: Push Meal Plan to External Todo Provider

  Scenario: IMealPlanPushTarget abstraction defines the contract
    Given the same multi-provider concern as shopping list push
    When the feature is designed
    Then an IMealPlanPushTarget interface is introduced with a PushAsync method
    And a TodoistMealPlanPushTarget implementation is the first provider
    And the Todoist API token is shared with the MEP-028 configuration (not re-prompted)

  Scenario: User configures a target Todoist project for meal plans
    Given meal plans may go to a different project than shopping lists
    When configuration is open
    Then the UI offers a dropdown of the user's Todoist projects
    And the selected project ID is saved independently of the shopping list project

  Scenario: User pushes a meal plan to Todoist
    Given a MealPlan with one or more MealPlanSlot rows assigned to Recipes
    And a valid Todoist token and project are configured
    When the user clicks "Push to Todoist" on the meal plan board
    Then each slot becomes a Todoist task titled "{MealType}: {Recipe.Title}"
    And the task due date matches the slot's date
    And tasks are created in the configured Todoist project

  Scenario: Swapping or deleting a slot updates the existing Todoist task on next push
    Given a previously-pushed MealPlan
    And the user has since swapped a recipe or cleared a slot
    When the user pushes again
    Then the corresponding Todoist tasks are updated (title change) or closed (slot cleared)
    And no orphaned tasks remain for removed slots

  Scenario: Todoist errors are surfaced as retryable
    Given the Todoist API is unreachable
    When the user pushes
    Then a clear error message is shown
    And the meal plan remains in a push-eligible state
```

## [MEP-030] Reorder Meal Plan to Prioritize Expiring Ingredients

**Status:** Done
**Priority:** Medium

### Implementation Notes
Shipped on branch `feature/mep-030-reorder-meal-plan-by-expiry`. Scope covered:

- `MealPlanReorderService` computes an urgency score per slot: for each recipe ingredient, `max(0, urgencyWindow − daysUntilExpiry) / urgencyWindow`. Already-expired ingredients contribute 1.0 (max). Count-of-expiring and remaining-days both feed into the same number, satisfying the AC without two separate axes.
- Reorder permutes `DayOfWeek` **within each `MealSlot`** — Breakfast recipes shuffle among Breakfast slots, etc. The AC flagged cross-meal-type reordering as TBD; deliberately out of scope to keep the user's meal rhythm intact.
- Stable sort: equal-urgency recipes retain their original relative day order.
- Two endpoints for explicit preview / commit: `POST /api/v1/meal-plans/{id}/reorder-by-expiry/preview` and `.../apply`. Both accept `?urgencyWindowDays=` with a default of 7. Non-positive values clamp to the default.
- Angular: "Reorder by expiry" action in the meal plan board header opens a preview dialog showing per-slot before → after assignments, the urgency window, and the per-slot urgency score. Confirm commits, Cancel backs out.
- No-op cases return a `Reason` string ("No planned recipes use ingredients expiring..." or "The current order already prioritizes...") that the dialog surfaces directly.

### Business Problem
MEP-007's meal plan generation considers waste-reduction in its initial candidate ranking, but that ranking is only as fresh as the moment of generation. Mid-week I buy produce, forget about leftovers, or have an ingredient sneak up on its expiry date -- and the existing plan no longer reflects the updated urgency. I want an explicit "reorder my planned meals to put expiry-consuming meals first" action that shuffles the slot dates within the existing plan (without regenerating or swapping recipes), so I cook the urgent stuff first. This is different from generating a new plan; it preserves every recipe the user already picked and only rearranges their sequencing.

### Acceptance Criteria
```gherkin
Feature: Reorder Meal Plan to Prioritize Expiring Ingredients

  Scenario: "Reorder by expiry" action is available on the meal plan board
    Given a MealPlan with at least two MealPlanSlot rows assigned to Recipes
    When the user views the meal plan board
    Then a "Reorder by expiry" action is visible
    And clicking it opens a preview of the proposed reorder

  Scenario: Service computes an expiry-urgency score per planned recipe
    Given each assigned Recipe has RecipeIngredient rows mapped to CanonicalIngredients
    And each CanonicalIngredient may have InventoryItem rows with ExpiryDate
    When the service computes the reorder
    Then each planned recipe receives a score based on how many of its ingredients are expiring within a configurable urgency window (default 7 days)
    And the score accounts for both count of expiring ingredients and remaining days until expiry

  Scenario: Slot dates are shuffled so higher-urgency recipes move earlier in the plan window
    Given a MealPlan spanning a date range (e.g., Mon through Sun)
    When the reorder is computed
    Then slot dates are reassigned so highest-urgency recipes occupy the earliest slots
    And recipes with equal urgency retain their relative ordering
    And no recipe moves outside the original MealPlan date range
    And MealType (Breakfast/Lunch/Dinner) is preserved per slot unless the user explicitly opts in to cross-meal-type reordering (TBD)

  Scenario: User previews before commit
    Given the service has proposed a reorder
    When the preview is displayed
    Then the user sees a side-by-side of current vs proposed slot dates
    And the user may confirm (apply the reorder) or cancel (no change)
    And confirming persists the new slot dates

  Scenario: Plans without any expiring-ingredient input are a no-op
    Given no assigned recipe has any ingredient expiring within the urgency window
    When the user clicks "Reorder by expiry"
    Then a clear message explains that nothing needs reordering
    And no slot dates are changed
```

## [MEP-031] Auto-Restore Inventory When a Consumed Meal is Unmarked

**Status:** Done
**Priority:** Medium
**Depends on:** MEP-027

### Implementation Notes
Shipped alongside MEP-027 (same PR / branch) because MEP-031 consumes the `ConsumeAuditEntry` audit trail MEP-027 creates.

- `DELETE /api/v1/meal-plan-slots/{id}/consume` reverses the consume. When the slot was consumed with auto-deplete true, `MealConsumptionService.UnconsumeAsync` replays each audited decrement: if `OriginalInventoryItemId` still points at an existing row, the deducted quantity is added back to that exact row so expiry tracking is preserved; if the row has been deleted, a fresh `InventoryItem` is created using the audited `OriginalLocation` and `OriginalExpiryDate`. Audit rows are deleted after restoration.
- State-only reversal when `ConsumedWithAutoDeplete == false`: clears `ConsumedAt` + `ConsumedWithAutoDeplete`, inventory untouched.
- Angular "Unmark" button on consumed slots calls the DELETE endpoint and updates the card in place.

### Business Problem
Paired with MEP-027. If `AutoDepleteOnConsume` is on and the user accidentally marks a meal as eaten -- or the household plan changes and a meal actually did not happen -- the inventory depletion must be symmetrically reversible. Without this, a single mis-click permanently subtracts ingredients and the user has to re-add them by hand. Unmarking a slot should restore the same ingredient quantities that were originally deducted, to the same `CanonicalIngredient` rows where possible, preserving expiry tracking.

### Acceptance Criteria
```gherkin
Feature: Auto-Restore Inventory When a Consumed Meal is Unmarked

  Scenario: Unmarking a consumed slot reverses the depletion only when auto-deplete was on at consume time
    Given a MealPlanSlot has ConsumedAt set
    And ConsumedWithAutoDeplete is true (preference was on at the time the slot was marked eaten)
    When the user unmarks the slot via the UI
    Then ConsumedAt is cleared
    And for each RecipeIngredient the service adds the consumed quantity back to inventory

  Scenario: Restored quantities go back to the same InventoryItem rows when possible
    Given the original consume recorded which InventoryItem rows were decremented and by how much (via a per-consume audit row)
    When the unmark runs
    Then the service attempts to add each quantity back to the same InventoryItem row
    And the row's expiry date is preserved

  Scenario: Restored quantities create a new InventoryItem row when the original row has been deleted
    Given an InventoryItem row that was previously decremented has since been deleted
    When the unmark runs
    Then a new InventoryItem row is created with the restored quantity
    And the ExpiryDate is copied from the audit row where possible
    And the Location is copied from the audit row where possible

  Scenario: Unmarking a slot that was consumed with auto-deplete OFF is a state-only change
    Given a MealPlanSlot has ConsumedAt set
    And ConsumedWithAutoDeplete is false
    When the user unmarks the slot
    Then ConsumedAt is cleared
    And no InventoryItem rows are modified
```

## [MEP-032] Settings Page with Bring-Your-Own Claude API Key and Graceful AI Degradation

**Status:** Done
**Priority:** High

### Implementation Notes
Shipped on branch `feature/mep-032-settings-page-and-byo-claude-api-key`. Scope covered:

- `Features/Settings/` with `SettingsController` exposing `GET /status`, `POST /token`, `POST /test`, `DELETE /token`. `IClaudeTokenStore` encrypts the Anthropic API key via ASP.NET DataProtection and writes to `%LOCALAPPDATA%/MealsEnPlace/settings/claude-token.dat` (key ring under `%LOCALAPPDATA%/MealsEnPlace/keys`). Responses only ever surface `{ configured: bool }` — the raw key never appears in any body or log line.
- `IClaudeAvailability` availability gate wired into `UnitOfMeasureNormalizationService` (defers unresolved tokens to the MEP-026 review queue), `RecipeImportService` (skips dietary classification; persists empty `RecipeDietaryTag` collection), `RecipeMatchingService` (skips the Claude feasibility / substitution pass and sets `ClaudeFeasibilityApplied = false` on the response), and `MealPlanService` (skips the Claude optimization pass).
- `AnthropicTestClient` issues a one-token `messages` request against `https://api.anthropic.com` for the Test Connection endpoint only. Failure surfaces the Anthropic error message without overwriting the persisted key.
- Angular `/settings` route with four sections (Display, AI, External Integrations stub, Inventory Behavior stub). AI section supports paste/save/test/remove with a confirmation dialog before deletion. `SettingsService` (HTTP) and `AiAvailabilityService` (app-wide signal) drive the state.
- Persistent `AiDisabledBannerComponent` renders above the app content when no key is configured. Session-dismissible; reappears on next app load until a key is saved.
- `RecipeMatchResultsComponent` shows a subtle in-page note when the match response's `claudeFeasibilityApplied` flag is false.
- **MEP-012 scenario** (flyer refusal): MEP-012 is not implemented yet; it must honor `IClaudeAvailability` when built (see the MEP-012 note below).

### Scope decisions
- **No API version bump.** Single-user local deployment; only in-repo Angular consumes the API. JSON shapes gained `claudeFeasibilityApplied` on `RecipeMatchResponse` in place.
- **Real Anthropic call for Test Connection only.** The other Claude-backed methods on `IClaudeService` remain stubs (they always have been). Converting them to real Claude calls is scheduled for future stories; MEP-032 lands the availability gate so those paths are skipped rather than invoked when no key is present.

### Business Problem
The app currently assumes a Claude API token is available at all times (configured via `dotnet user-secrets` during development). If the app is distributed to anyone beyond the original developer -- or if the developer ever runs without a token configured -- every AI-backed path breaks in unclear ways. Two related needs:

1. **Bring-your-own Claude auth.** Users must be able to paste their own Anthropic API key into a settings page, have the app verify it works, and persist it securely across restarts. The key is never shipped with the app, never stored in plaintext, never exposed through any API response.

2. **Graceful degradation without AI.** When no key is configured (or the configured key fails), the app must still function end-to-end for every non-AI path. AI-specific features disable cleanly with clear user-facing explanations of what is and is not available.

This story also lands the consolidated settings page as a UI scaffold, so future BYO-credential settings (Todoist API token from MEP-028/029, `AutoDepleteOnConsume` toggle from MEP-027, store-sale opt-in from MEP-012, etc.) have a home to plug into.

### Acceptance Criteria
```gherkin
Feature: Settings Page with Bring-Your-Own Claude API Key and Graceful AI Degradation

  Scenario: Settings page exists as a dedicated route with navigable sections
    Given the Angular frontend does not currently have a consolidated settings page
    When the feature is built
    Then a /settings route and Angular component are added, linked from the main navigation
    And the page is organized into sections (Display, AI, External Integrations, Inventory Behavior) so future BYO-credential stories have a clear home

  Scenario: User pastes a Claude API key into the AI section
    Given the AI section of the settings page
    When the user enters a key into a password-style input and clicks Save
    Then a POST /api/v1/settings/claude/token endpoint receives the key
    And the backend persists the key using ASP.NET DataProtection (or equivalent) to an encrypted local file outside the repo
    And the response body does NOT include the key -- only a { configured: true } indicator

  Scenario: Test Connection verifies the key against the Anthropic API
    Given a key has been entered
    When the user clicks "Test connection"
    Then the backend invokes a cheap Claude API call (e.g., a small messages request) using the entered key
    And returns success / failure with the Anthropic-reported error message on failure
    And an invalid key does not overwrite a previously-valid stored key

  Scenario: Configured key persists across app restarts
    Given a valid key has been saved
    When the app is restarted
    Then subsequent Claude-backed operations read the key from the encrypted local store
    And the settings page reflects { configured: true } without re-prompting

  Scenario: User can clear the stored key
    Given a configured key exists
    When the user clicks "Remove key" in the AI section
    Then the stored key is deleted from the local store
    And the next call to any Claude-backed operation enters the degraded path described below
    And a confirmation prompt appears before deletion so a mis-click is recoverable

  Scenario: API never leaks the key in any response
    Given a configured key exists
    When any endpoint returns settings, user preferences, or diagnostic info
    Then the response contains at most { configured: true } or a masked indicator
    And the raw key value is never present in any HTTP response body, header, or log line

  Scenario: UOM normalization degrades gracefully without a key
    Given no Claude API key is configured
    When UomNormalizationService encounters a measure string that does not resolve via abbreviation, name, alias, or count-noun fallback
    Then the service routes the ingredient to the MEP-026 review queue instead of attempting a Claude call
    And the ingredient is preserved in its raw form with a flag indicating user resolution is required

  Scenario: Recipe dietary classification degrades gracefully without a key
    Given no Claude API key is configured
    When a recipe is imported or created
    Then no dietary tags are auto-assigned
    And the recipe is persisted normally with an empty RecipeDietaryTag collection
    And the user may manually apply tags via the existing recipe edit UI

  Scenario: Recipe matching Claude feasibility pass is skipped without a key
    Given no Claude API key is configured
    And the user invokes "What can I make?"
    When the matching pipeline runs
    Then the deterministic ranking (matched / total ingredients, expiry bonus) produces the result list
    And the Claude-backed feasibility and substitution pass is skipped
    And the UI shows a subtle note that AI-suggested substitutions are unavailable

  Scenario: Meal plan optimization Claude review is skipped without a key
    Given no Claude API key is configured
    And the user generates a meal plan
    When the plan is produced
    Then the deterministic ranking (waste score, seasonal affinity, dietary filter, recency) drives selection
    And the Claude-backed variety-and-waste-optimization pass is skipped
    And the plan is persisted normally

  Scenario: Store sale ingest requires Claude Vision and refuses cleanly without a key
    Given no Claude API key is configured
    And the user attempts to import a flyer PDF (MEP-012)
    When the ingest action runs
    Then a clear message explains that flyer parsing requires Claude Vision
    And the message links to the AI section of the settings page
    And no partial StoreSale rows are persisted

  Scenario: Persistent UI indicator when AI is disabled
    Given no Claude API key is configured
    When the user is anywhere in the app
    Then a subtle badge or banner (e.g., in the header or nav) indicates "AI features disabled"
    And the badge links to the AI section of the settings page
    And the badge is dismissible but reappears on next app load until a key is configured

  Scenario: Non-AI features continue to work identically with or without a key
    Given no Claude API key is configured
    When the user exercises: inventory CRUD, container-reference detection, deterministic UOM lookup, recipe manual entry, recipe import from TheMealDB (no Claude classification step), shopping list generation, seasonal produce view, waste alerts, meal plan manual editing, display-system toggle, dark mode
    Then every listed feature functions identically to the key-configured experience
    And no silent failures or unexplained empty results occur
```

## [MEP-033] Remove TheMealDB Integration

**Status:** Done
**Priority:** Low
**Depends on:** MEP-026 (Kaggle ingest) must land first so the app has a working catalog source before TheMealDB is removed.

### Implementation Notes
Shipped on branch `feature/mep-033-remove-themealdb-integration`. Scope covered:

- Deleted `src/MealsEnPlace.Api/Infrastructure/ExternalApis/TheMealDb/` (4 files) and `tests/MealsEnPlace.Unit/Infrastructure/TheMealDbClientTests.cs` in full.
- Pruned `IRecipeImportService` / `RecipeImportService` / `RecipeImportController` to the generic CRUD surface: `CreateRecipeAsync`, `GetAllLocalRecipesAsync`, `GetRecipeDetailAsync`. Removed `ImportByIdAsync`, `SearchAsync`, `SearchByCategoryAsync` and the three endpoints (`POST /import/{mealDbId}`, `GET /search`, `GET /search/category`).
- Deleted `RecipeSearchResultDto.cs` and `RecipeImportResultDto.cs`. Removed the unused `IUnitOfMeasureNormalizationService` dependency from `RecipeImportService` (flagged by an unread-parameter warning after the prune).
- Removed `Recipe.TheMealDbId` and its fluent filtered unique index. Migration `20260420005436_DropTheMealDbIdColumn` drops the column + index with a symmetric `Down`; smoke-tested Up + Down + Up against local Postgres. Recipe rows preserved.
- Program.cs: dropped the `ITheMealDbClient` DI registration, the `TheMealDb` named `HttpClient`, and the `Infrastructure.ExternalApis.TheMealDb` using. `Tools.Ingest/Program.cs` no longer initializes the removed field.
- Angular: deleted `recipe-import.component.ts`, the `/recipes/import` route, the "Import Recipes" nav link, the "Import Recipes" button on the recipe browser, the `importRecipe`/`searchByQuery` methods on `RecipeService`, and the two TypeScript DTO interfaces.
- C4 diagrams: `context.puml` and `container.puml` drop the `TheMealDB API` external system; `component-api.puml` drops the `TheMealDB Client` component; `component-web.puml` drops the `Recipe Import` Angular component. The Recipe component description now describes the Kaggle-driven interactive surface.
- Sweep: `README.md`, `CLAUDE.md`, and the Recipes feature READMEs (API + Web) rewritten to reference the Kaggle ingest as the catalog source. MEP-004 entry gets a supersession note below.
- Test delta: 40 TheMealDB-specific tests removed from `RecipeImportServiceTests.cs`, 8 from `RecipeImportControllerTests.cs`. Remaining: 459 unit + 18 integration, 0 skipped.
- Grep gate: no `TheMealDB` / `TheMealDb` / `themealdb` tokens remain in `src/` or `docs/` outside the frozen EF migration snapshots, the drop migration itself, and intentional historical-context comments on `IRecipeImportService` / `RecipeImportService` / `RecipeImportController`.

### Business Problem
TheMealDB was chosen early as the recipe catalog source because it is free, open, and required no auth. Its ~600-recipe catalog turned out to be the gating constraint that drove MEP-025 (the spike to evaluate larger sources) and MEP-026 (the Kaggle 2M ingest). Once the Kaggle path is working, TheMealDB's catalog is superfluous and its integration code is pure maintenance burden: the HTTP client, DTOs, import service, UI import flow, tests, C4 diagram node, README copy, and the `TheMealDbId` column on the `Recipe` entity all exist for a source the user no longer plans to pull from.

This story removes that surface area in one coherent sweep. Recipes previously imported from TheMealDB are preserved in the database (they are just rows in the `Recipe` table at this point) -- only the import path, the dedicated identifier column, and the ancillary scaffolding go away.

### Acceptance Criteria
```gherkin
Feature: Remove TheMealDB Integration

  Scenario: TheMealDB client code is deleted
    Given the src/MealsEnPlace.Api/Infrastructure/ExternalApis/TheMealDb/ folder exists
    When this story is implemented
    Then the entire folder is removed from the repository
    And all corresponding unit and integration tests are removed
    And any DI registrations for TheMealDbClient or ITheMealDbClient are removed from Program.cs

  Scenario: TheMealDB import controller and service are deleted
    Given Features/Recipes contains a TheMealDB-specific import flow
    When this story is implemented
    Then any controllers, services, DTOs, request/response types exclusive to the TheMealDB import are removed
    And the generic recipe-import service (used by MEP-018 manual entry and MEP-026 bulk ingest) remains intact
    And Swagger no longer lists any TheMealDB-specific endpoint

  Scenario: TheMealDbId column on Recipe is removed via migration
    Given the Recipe entity has a TheMealDbId property and column
    When this story is implemented
    Then an EF Core migration drops the TheMealDbId column from the Recipes table
    And the Recipe entity no longer exposes the TheMealDbId property
    And existing Recipe rows are preserved -- only the column is removed
    And existing Recipe rows that were originally sourced from TheMealDB continue to function normally in matching, meal plans, and shopping lists

  Scenario: Angular frontend loses any TheMealDB import UI
    Given the recipe browser / import pages reference a TheMealDB import flow
    When this story is implemented
    Then the corresponding Angular components, services, and routes are removed
    And no dead code, unused imports, or stale feature flags remain
    And the manual recipe entry UI (MEP-018) and the bulk-ingest tooling UI (MEP-026) continue to function

  Scenario: C4 diagrams remove TheMealDB as an external system
    Given docs/c4/context.puml and container.puml reference TheMealDB as an external system
    When this story is implemented
    Then TheMealDB is removed from the C4 .puml sources
    And the render-c4 workflow produces updated PNGs
    And the README architecture section reflects the removal

  Scenario: README and CLAUDE.md reflect the removal
    Given several documentation files reference TheMealDB as the recipe data source
    When this story is implemented
    Then README.md tech stack, project structure, and feature sections are updated
    And CLAUDE.md external APIs section is updated
    And per-feature README files under Features/Recipes/ are updated
    And no stale reference to TheMealDB remains in any tracked file (verified via a grep step in review)

  Scenario: Backlog is updated to reflect the historical record
    Given MEP-004 (Recipe Library Import) is marked Done based on a TheMealDB implementation
    When this story is implemented
    Then MEP-004 remains marked Done (the historical outcome stands) but gains a brief note that the TheMealDB implementation was superseded by MEP-026
    And no other backlog items referencing TheMealDB as an active dependency remain

  Scenario: Full test suite passes after removal
    Given the sweep is complete
    When dotnet test is run
    Then all remaining tests pass
    And code coverage does not regress below the 90% pre-PR threshold
    And any tests that were TheMealDB-only are removed (not skipped or commented out)
```

## [MEP-034] Retroactive Rename: UOM / Uom → UnitOfMeasure Across Codebase

**Status:** Done
**Priority:** Low

### Implementation Notes
Shipped as a single PR on branch `feature/mep-034-uom-to-unit-of-measure-rename`. Scope covered:

- All C# identifiers (types, interfaces, methods, properties, local variables) and matching file names across `src/` and `tests/`, except frozen `Migrations/` snapshots which intentionally preserve the historical `Uom*` names.
- Angular TypeScript models, components, services, and matching `formControlName` / column-def / CSS-class identifiers (`uom-field` → `unit-of-measure-field`).
- Database column renames via migration `20260419202941_RenameUomColumnsToUnitOfMeasure` using `RenameColumn` + `RenameIndex` (`UomId` → `UnitOfMeasureId` on `InventoryItems`/`RecipeIngredients`/`ShoppingListItems`; `DefaultUomId` → `DefaultUnitOfMeasureId` on `CanonicalIngredients`; `BaseUomId` + `UomType` on `UnitsOfMeasure`). The Down path reverses every rename. Smoke-tested Up and Down against a local Postgres with seeded and ingested rows.
- JSON property names: API response shapes now emit `unitOfMeasureId` / `unitOfMeasureType` / `unitOfMeasureAbbreviation` (camelCase auto-derived from the renamed C# properties). **No API version bump**: single-user local deployment, only in-repo Angular consumes the API, so properties renamed in place and Angular updated in lockstep within the same PR.
- URL routes: AC scenario 6 became a no-op. All `/api/v1/...` routes were already spelled out (e.g. `unit-of-measure-review-queue` from MEP-026 Option B). No redirects required.
- Docs: CLAUDE.md caveat about legacy `Uom...` naming removed (no longer applies). C4 diagrams (`component-api.puml`, `container.puml`, `context.puml`), feature READMEs, and agent definition files updated.


### Business Problem
Early in the project, "unit of measure" was abbreviated as `UOM` / `Uom` in C# class names (`UomNormalizationService`, `UomDisplayConverter`, `UomConversionService`, `UomType`, `UomAbbreviation`), database columns (`UomId`, `DefaultUomId`), DTO properties, Angular models, and API response bodies. The project convention has since shifted to "avoid abbreviations in domain names; spell out terms like `UnitOfMeasure`" (see CLAUDE.md and MEP-026 Phase 5d notes). New code written under MEP-026 uses the spelled-out form; the legacy surface area still uses the abbreviated form.

This story is the retroactive cleanup to make the whole codebase consistent. It is deliberately scoped as a standalone story because it touches the database schema (column renames require EF Core migrations with production implications), public API response shapes (clients would need to update), and every consumer of the legacy types. None of those changes are urgent, so the cleanup is kept as a low-priority backlog item rather than smuggled into an unrelated PR.

### Acceptance Criteria
```gherkin
Feature: Retroactive Rename UOM to UnitOfMeasure Across Codebase

  Scenario: C# class, interface, method, and file names are spelled out
    Given existing types like UomNormalizationService, UomDisplayConverter, UomConversionService, UomType, UomAbbreviation
    When the rename runs
    Then every "Uom" token in C# identifiers becomes "UnitOfMeasure"
    And corresponding .cs files are renamed accordingly
    And no "Uom"-prefixed identifier remains in src/ (verified via grep gate in review)

  Scenario: Database columns are renamed via EF Core migration
    Given existing columns such as RecipeIngredient.UomId, CanonicalIngredient.DefaultUomId
    When a new migration is generated
    Then the migration uses RenameColumn to change UomId to UnitOfMeasureId and similar spelled-out forms
    And existing row data is preserved
    And a rollback path (Down) restores the original column names

  Scenario: DTOs and API response bodies use spelled-out names
    Given existing response DTOs with UomId / UomAbbreviation / UomType fields
    When the rename runs
    Then the JSON property names become UnitOfMeasureId / UnitOfMeasureAbbreviation / UnitOfMeasureType
    And Swagger / OpenAPI docs reflect the new shapes
    And the API version is bumped (or a migration strategy documented) so clients know to update

  Scenario: Angular models and components follow the rename
    Given existing TypeScript files referencing uomId, uomAbbreviation, UnitOfMeasureDto
    When the rename runs
    Then the TypeScript identifiers spell out UnitOfMeasure
    And ng build passes with zero errors
    And ng lint passes with zero warnings

  Scenario: Tests are updated in lockstep
    Given existing unit and integration tests referencing Uom*-named types and properties
    When the rename runs
    Then every test compiles and passes
    And the total passing test count is preserved (no silent skips)

  Scenario: Legacy URL routes are updated or aliased
    Given URL routes that embed "uom" (e.g. /api/v1/uom-review-queue)
    When the rename runs
    Then the routes are updated to spelled-out kebab-case form (/api/v1/unit-of-measure-review-queue)
    And old routes either return 301 redirects or are removed (documented decision)

  Scenario: Migration script is smoke-tested against a populated database
    Given a local dev database with real ingested data (MEP-026)
    When the rename migration is applied
    Then no data loss occurs
    And application endpoints function identically against the renamed schema
    And rolling the migration back restores the original column names without data loss
```

## [MEP-035] Todoist Settings UI: Token Entry, Project Picker, Test Connection

**Status:** Done
**Priority:** Medium
**Depends on:** MEP-028 (the push surface must exist first)

### Implementation Notes
Scope delivered:

- `TodoistTokenStore` mirrors `ClaudeTokenStore`: DataProtection-encrypted file at `%LOCALAPPDATA%/MealsEnPlace/todoist-token.dat` with distinct purpose `"MealsEnPlace.TodoistToken.v1"` so ciphertexts are not interchangeable with the Claude token. The key ring at `%LOCALAPPDATA%/MealsEnPlace/keys/` is shared.
- `ITodoistTokenResolver` — single source of truth. Encrypted store wins; `Todoist:Token` user secret is the fallback. Consumed by `TodoistClient`, both push targets, the Settings status endpoint, and the Test Connection endpoint.
- Four new endpoints on `SettingsController`: `POST /todoist/token` (save), `POST /todoist/test` (live `GET /rest/v2/projects` via new `TodoistTestClient`), `DELETE /todoist/token`. Status endpoint now consults the resolver rather than `TodoistOptions.IsConfigured` (removed).
- Frontend: External Integrations card stub replaced with a live Todoist subsection matching the AI (Claude) section (password input, status pill, Save / Test connection / Remove token, `ConfirmDialogComponent` before remove). `TodoistAvailabilityService` gained `setConfigured` for optimistic updates after save/remove.
- Tests: 24 new tests — `TodoistTokenStoreTests` (6, including a ciphertext-isolation test across the two providers), `TodoistTokenResolverTests` (6), `TodoistTestClientTests` (4), plus 8 new `SettingsControllerTests` scenarios covering the Todoist endpoints and the "failed candidate never overwrites stored" rule. Existing push-target and client tests adapted to the resolver constructor.
- Error message on push targets updated from "Set the Todoist:Token user secret." to "Save a Todoist API token from the Settings page."

### Deferred (scope decisions)
- Removing the user-secret fallback entirely — kept for now so existing local setups don't break. Migration note: once the Settings UI has been in general use for a release cycle, drop the `TodoistOptions.Token` binding and simplify `TodoistTokenResolver` to read only from the store.
- Non-Windows hardening of the DataProtection key ring — tracked separately as MEP-039.

### Business Problem
MEP-028 and MEP-029 ship the Todoist push flow by reading the API token from `dotnet user-secrets` (`Todoist:Token`) and a fixed project ID from `Todoist:ProjectId`. That works for single-developer local use but doesn't match the bring-your-own-credential pattern MEP-032 established: the user should be able to paste their token into the Settings page, have it verified against Todoist, and persist it via ASP.NET DataProtection the same way the Claude key is stored. This story closes that gap and brings Todoist into the standard External Integrations section of the Settings page.

### Acceptance Criteria
```gherkin
Feature: Todoist Settings UI

  Scenario: External Integrations section exposes a Todoist subsection
    Given the Settings page already has an "External Integrations" card (stubbed)
    When this story is implemented
    Then the card renders a Todoist subsection with paste-to-save, test-connection, and remove-key affordances
    And the visual language matches the AI (Claude) section (password-style input, status pill, confirm-before-remove)

  Scenario: Token is stored via ASP.NET DataProtection at rest
    Given the user pastes a Todoist API token
    When they click Save
    Then the backend persists the encrypted token to a local file under %LOCALAPPDATA%/MealsEnPlace/settings/
    And the response body carries only { configured: true }
    And no endpoint ever returns the raw token value

  Scenario: Test Connection issues a GET /projects call
    Given a candidate or persisted token
    When the user clicks "Test connection"
    Then the backend calls Todoist's REST API v2 `GET /projects` with the supplied token
    And returns success or the Todoist-reported error message
    And an invalid token does not overwrite a previously-valid stored token

  Scenario: Fallback from user-secrets is preserved
    Given a token exists in both dotnet user-secrets (legacy) and the encrypted store
    When the Todoist push runs
    Then the encrypted-store value takes precedence
    And the user-secrets fallback continues to work when no encrypted value is present
    And a future migration note documents removing the user-secrets path once the settings UI is in general use
```

## [MEP-036] Surface Associated Todoist Project IDs for Push Target Selection

**Status:** Done
**Priority:** Medium
**Depends on:** MEP-028 (the `ExternalTaskLink` table must exist first), MEP-035 (the Todoist token resolver and `GET /rest/v2/projects` client)

### Implementation Notes
Shipped across backend, frontend, tests, and docs.

- `GET /api/v1/settings/todoist/projects/history` (`TodoistProjectHistoryService`) returns the distinct non-null `ExternalProjectId` values for Provider = "Todoist", always Inbox-first, with display names resolved through one `GET /api/v1/projects` call via the new `ITodoistProjectClient` (migrated from `/rest/v2/` by MEP-042). Degradation is a first-class path: an unreachable, failing, or unconfigured Todoist returns 200 with null display names and `namesResolved: false` — never a 5xx.
- Last-used project is **derived**, not stored: the newest `ExternalTaskLink` row per `SourceType` yields `lastUsedShoppingListProjectId` / `lastUsedMealPlanProjectId`. No new column and **no migration**, and the value cannot drift from what was actually pushed.
- Optional `{ projectId }` body (`TodoistPushRequest`) on the three push endpoints overrides `Todoist:ProjectId` for that push only. Omitting the body preserves the pre-MEP-036 fallback chain exactly, which is the regression path existing users hit.
- Frontend: `TodoistProjectPickerDialogComponent` (shared) opens between the "Push to Todoist" click and the push on both surfaces. It takes a `resourceType` and reads the matching `lastUsed*` off the response; a remembered project absent from the list falls back to Inbox.
- Tests: 605 unit (up from 598) + 18 integration, all green. Coverage 93.9% line / 81.9% branch.

### Scope decisions made during implementation
- **Server-derived recall beat client-side storage.** The frontend initially kept the last-used project in `localStorage`. That was removed: it duplicated a fact the API already derives from push history, and the two silently diverge when site data is cleared or the user pushes from another browser. The server is the single source of truth.
- **Project names resolve lazily on dialog open**, per the pre-implementation scope decision above. Confirmed correct during build: the Todoist create-task response carries `project_id` but no project name, so denormalizing a name onto `ExternalTaskLink` would have required the same `GET /projects` call plus a migration plus rename-staleness handling.

### Known gap (not introduced here)
The Angular dialog's spec file cannot execute — the frontend has no test runner configured. Tracked as MEP-041.
**Closed by MEP-041.** The Vitest runner is in place and this spec runs and passes unchanged.

### Business Problem
MEP-028 and MEP-029 push shopping lists and meal plans to Todoist, targeting whichever project is configured via the `Todoist:ProjectId` user secret (or Inbox when unset). That override is static — the user has to edit the user secret and restart the app every time they want to aim pushes at a different project. The Angular "Push to Todoist" buttons on the shopping list page and meal plan board fire the push immediately, with no opportunity to choose a destination.

The push flow should offer a lightweight project picker before sending. Every push already records the `ExternalProjectId` it used on the `ExternalTaskLink` row, so the app can enumerate the projects it has actually pushed to from local data alone. That history-based list is more relevant than a full dump of every Todoist project (which would include projects the user never intends to use for groceries or meals), and it keeps the picker short.

### Scope decisions
- **Project names are resolved lazily when the dialog opens.** `ExternalTaskLink.ExternalProjectId` stores only the raw Todoist ID (e.g. `"2331547980"`), and no name is recoverable from the push path: `TodoistTaskPayload` is the request type, and `TodoistClient` deserializes the create-task response into a shape that reads only `Id`. Todoist's REST v2 create-task response carries `project_id`, not a project name. Denormalizing a name onto `ExternalTaskLink` would therefore still require a `GET /rest/v2/projects` call at push time, plus a migration, plus staleness handling on rename — paying all of that for a call it does not avoid. Resolving on dialog open buys the same names for one call, with no schema change and no staleness.
- **The remembered selection is tracked per resource type.** Shopping lists and meal plans each remember their own last-used project, consistent with MEP-029's original AC and with routing groceries and meal prep to separate Todoist projects.
- **Name resolution is server-side and best-effort.** The history endpoint merges local IDs with live names in one round trip rather than making the Angular client orchestrate two calls. A failed or unconfigured Todoist call degrades to raw IDs rather than blocking the picker.
- **Out of scope:** a full dropdown of every Todoist project. A future story may add one alongside this history-based list; this story does not.

### Acceptance Criteria
```gherkin
Feature: Associated Todoist Project Quick-Pick

  Scenario: Push button opens a project-selection dialog instead of pushing immediately
    Given the user is on the shopping list page or the meal plan board
    And a Todoist token is configured
    When the user clicks "Push to Todoist"
    Then a lightweight dialog appears before the push executes
    And the dialog lists previously-used project targets plus "Inbox (default)"
    And confirming the selection triggers the push to that project
    And dismissing the dialog performs no push

  Scenario: Previously-used projects are enumerated from ExternalTaskLink rows
    Given ExternalTaskLink rows exist with Provider = "Todoist" and varying ExternalProjectId values
    When the dialog requests the available push targets
    Then the response contains the distinct non-null ExternalProjectId values for that provider
    And "Inbox (default)" is always present regardless of whether any null-project rows exist
    And the query uses the existing index on (Provider, ExternalProjectId) so it stays fast as history grows

  Scenario: History endpoint resolves project names in a single round trip
    Given distinct project IDs exist in the local push history
    When the client calls GET /api/v1/settings/todoist/projects/history
    Then the backend reads the distinct IDs from ExternalTaskLink
    And issues one GET /api/v1/projects call through the MEP-035 token resolver to map IDs to names
    And returns each entry with its project ID and resolved display name

  Scenario: Name resolution degrades to raw IDs when Todoist is unreachable
    Given the local push history contains project IDs
    And the Todoist API call fails or no token is configured
    When the history endpoint responds
    Then each entry still carries its project ID with a null display name
    And the response flags that names were not resolved
    And the dialog renders the raw IDs and a hint that names could not be loaded
    And the user can still select a project and complete the push

  Scenario: Selected project overrides the static Todoist:ProjectId for this push
    Given the user selects project "2331547980" in the dialog
    When the push executes
    Then every task in this push targets project "2331547980"
    And the ExternalTaskLink rows written by the push record "2331547980" as ExternalProjectId
    And the Todoist:ProjectId user secret is not modified

  Scenario: First-ever push has no history and offers only Inbox
    Given no ExternalTaskLink rows exist for Provider = "Todoist"
    When the user clicks "Push to Todoist"
    Then the dialog shows only "Inbox (default)"
    And confirming pushes to Inbox
    And the resulting ExternalTaskLink rows record ExternalProjectId = null

  Scenario: The dialog remembers the last selection per resource type
    Given the user previously pushed a shopping list to project "2331547980"
    And the user previously pushed a meal plan to project "9988776655"
    When the user opens the push dialog from the shopping list page
    Then "2331547980" is pre-selected
    When the user opens the push dialog from the meal plan board
    Then "9988776655" is pre-selected
    And a remembered project that no longer appears in the resolved project list falls back to "Inbox (default)"

  Scenario: Relationship to MEP-035
    Given MEP-035 shipped Todoist token entry, Test Connection, and remove-token, but no project picker
    When this story is implemented
    Then the history-based quick-pick is the primary project selection mechanism
    And it reuses the MEP-035 token resolver rather than introducing a second credential path
    And a full live-API project dropdown remains a possible future story, out of scope here
```

## [MEP-037] Strip Ad / Tracking URLs on Recipe Ingest

**Status:** Backlog
**Priority:** Low
**Depends on:** MEP-026 (ingest pipeline)

### Business Problem
A subset of Kaggle recipe rows carry `link` values that are ad-tracking redirect chains (e.g., `googleads.g.doubleclick.net/pcs/click?...&adurl=...`) or affiliate-wrapper URLs rather than the canonical recipe page. Today the ingest stores the raw link (or drops it when it exceeds the 2000-char column cap — MEP-026 hotfix). Both outcomes are wrong for the user: an ad-tracking URL either clicks through to a tracker before redirecting, or is a broken promise in the UI.

The ingest should detect the well-known tracker/ad host patterns, attempt to extract the nested real URL from the query string when available (e.g., the `adurl` / `url` / `q` / `destination` parameters), and fall back to null when no canonical URL can be recovered. The `IngestSummary` should report how many links were rewritten and how many were dropped as tracker-only, so the user can gauge dataset quality over time.

Out of scope: fetching the URL to confirm it resolves, or unwrapping user-facing short links (bit.ly, t.co) that require a network round-trip. Those belong in a separate "link health pass" story if ever justified.

### Acceptance Criteria
```gherkin
Feature: Strip Ad / Tracking URLs on Recipe Ingest

  Scenario: Known tracker host with a nested real URL is unwrapped
    Given a Kaggle row whose link is "https://googleads.g.doubleclick.net/pcs/click?xai=...&adurl=https%3A%2F%2Fwww.foodnetwork.com%2Frecipes%2Fchicken-pot-pie"
    When the ingest processes the row
    Then Recipe.SourceUrl stores the unwrapped "https://www.foodnetwork.com/recipes/chicken-pot-pie"
    And IngestSummary.TrackingLinksUnwrapped is incremented

  Scenario: Tracker URL with no recoverable destination is dropped
    Given a Kaggle row whose link is a tracker URL with no nested destination parameter
    When the ingest processes the row
    Then Recipe.SourceUrl is null
    And IngestSummary.TrackingLinksDropped is incremented
    And the recipe itself still imports (the link is optional)

  Scenario: Clean recipe URL passes through untouched
    Given a Kaggle row whose link is a direct recipe URL on a known recipe host
    When the ingest processes the row
    Then Recipe.SourceUrl matches the original link byte-for-byte
    And neither tracker counter is incremented

  Scenario: Backfill pass updates previously-ingested rows
    Given a Kaggle ingest completed before this story shipped
    When the user runs the ingest tool with a `--rewrite-tracking-links` flag
    Then existing Recipe rows whose SourceUrl matches a tracker pattern are rewritten in place (or nulled)
    And a summary reports how many rows were changed
```

## [MEP-038] Canonical Ingredient Deduplication Pass

**Status:** Done
**Priority:** Medium
**Depends on:** MEP-026 (ingest creates the CanonicalIngredient rows this story folds)

### Implementation Notes
Shipped as a new offline CLI tool `MealsEnPlace.Tools.Dedup` mirroring the `Ingest` tool's shape. Scope covered:

- `CanonicalNameNormalizer` — pure string normalizer. Lowercases, strips a narrow stopword list (cosmetic prep / state modifiers like `chopped`, `diced`, `fresh`), conservatively singularizes English plurals (`-s` / `-es` / `-oes` / `-ies` / `-ches` / `-shes` / `-sses` / `-xes`), and sorts remaining tokens. Size-significant modifiers (`baby`, `mini`, `jumbo`, `smoked`, `pickled`) are intentionally NOT stopwords so "baby carrot" stays distinct from "carrot".
- `FoldGroupResolver` — pure function over `CanonicalIngredientFoldCandidate`s. Groups by normalized key. Survivor per group: shortest name → highest `ReferenceCount` → alphabetical. Single-member groups and empty-key candidates are skipped.
- `CanonicalIngredientDedupRunner` — orchestrates. Loads every `CanonicalIngredient` with per-table FK counts (RecipeIngredient / InventoryItem / ShoppingListItem / SeasonalityWindow / ConsumeAuditEntry). Applies in batches of `DedupConstants.FoldGroupBatchSize` inside explicit transactions, using `ExecuteUpdateAsync` for bulk FK reassignment and `ExecuteDeleteAsync` for loser cleanup.
- New `CanonicalIngredientAliases` table captures folded-away names (schema mirrors `UnitOfMeasureAlias`; 200-char `Alias`, FK to survivor, non-unique index on `Alias`). Migration `20260421105129_AddCanonicalIngredientAlias` is Up/Down tested.
- `--dry-run` mode populates the full summary (fold count, alias inserts, per-table FK reassignment projection) without writing.
- Tests: 35 total across unit tests for the normalizer and resolver plus three SQLite-backed integration-style tests for the runner (no-op, dry-run, live fold). SQLite in-memory is used because the EF InMemory provider supports neither `ExecuteUpdate` nor explicit transactions.

### Deferred (scope decisions)
- Claude-assisted merge for the long tail (ambiguous merges, size-dependent distinctions like "grape tomato" vs "tomato") — explicitly out of scope per the story text; can file as a follow-on if the heuristic pass leaves meaningful residue.

### Business Problem
The MEP-026 ingest creates one `CanonicalIngredient` per unique NER token. A 1.64M-recipe live run produced **146,584** canonical rows — roughly 10× the MEP-025 projection of 5k-15k. The variance is morphological noise in the Kaggle NER column: "onion", "chopped onion", "diced onion", "red onion", "onions" all become distinct canonicals. That breaks recipe matching in a user-visible way: an inventory entry for "1 onion" won't match a recipe calling for "2 cups chopped onion" even though the user clearly has the ingredient.

Need a heuristic dedup pass that folds modifier-only duplicates to a single survivor row and updates every `RecipeIngredient.CanonicalIngredientId` FK that pointed at a loser. The goal is cheap matching-quality wins without calling Claude on 146k rows — a strong stopword list (chopped, diced, sliced, minced, fresh, dried, whole, raw, cooked, large, small, medium, etc.) plus singular/plural collapse should cover the bulk.

A Claude-assisted pass for the long tail (ambiguous merges, size-dependent distinctions like "baby carrots" vs "carrots") is a separate follow-up — not scoped here.

### Acceptance Criteria
```gherkin
Feature: Canonical Ingredient Deduplication Pass

  Scenario: Modifier-stripped duplicates fold to one survivor
    Given CanonicalIngredient rows exist for "onion", "chopped onion", "diced onion", and "onions"
    When the user runs the dedup tool
    Then exactly one survivor row remains (the shortest / most generic name)
    And every RecipeIngredient that pointed at a folded row now points at the survivor
    And a summary reports the number of rows folded and FKs updated

  Scenario: True distinct ingredients are preserved
    Given CanonicalIngredient rows exist for "carrot" and "baby carrot"
    When the dedup tool runs with size-preserving mode
    Then "baby carrot" is NOT folded into "carrot"
    And a configurable modifier allow-list controls which qualifiers are size-significant vs cosmetic

  Scenario: Dry-run reports the intended merges without writing
    Given a populated CanonicalIngredient table
    When the user runs the dedup tool with --dry-run
    Then the tool prints the proposed fold groups and affected FK counts
    And no database rows are modified

  Scenario: Non-destructive record of the original NER token
    Given a CanonicalIngredient row is folded into a survivor
    When the fold happens
    Then the folded-away name is appended to the survivor's alias / synonym list (exact schema TBD at implementation time)
    And the fold is auditable / reversible via that synonym list
```

## [MEP-039] Harden DataProtection Key Ring on macOS and Linux

**Status:** Post-MVP (Backlog)
**Priority:** Low
**Depends on:** MEP-032 (Claude token), MEP-035 (Todoist token) — the consumers of the key ring

### Business Problem
ASP.NET DataProtection is used to encrypt the Claude API key (MEP-032) and the Todoist API token (MEP-035) at rest. On Windows, `PersistKeysToFileSystem` transparently wraps the key-ring files with DPAPI, which binds them to the current Windows user account. On macOS and Linux, no such wrapping is applied by default: the key-ring XML files sit on disk protected only by filesystem permissions. If a second local account (or a backup, or a mis-scoped Docker volume mount) gains read access to `~/.local/share/MealsEnPlace/keys/`, the attacker can decrypt every token the app has stored.

For a single-user local tool this is acceptable at MVP, but it's a meaningful delta in the security posture between platforms. Post-MVP we should close the gap so the non-Windows experience matches Windows.

Options to evaluate at implementation time:
- `ProtectKeysWithCertificate` using a locally-generated cert whose private key is stored in the platform keystore (macOS Keychain / libsecret on Linux).
- A community package such as `AspNetCore.DataProtection.Keychain` (macOS) or a `libsecret`-backed key XML encryptor (Linux).
- As a lighter-weight fallback, enforce `chmod 700` on the keys directory at startup and warn loudly when the permission check fails.

Story should also document the current gap in the project README so users running the app outside Windows understand the tradeoff until this lands.

### Acceptance Criteria
```gherkin
Feature: Harden DataProtection Key Ring on macOS and Linux

  Scenario: Key-ring files are encrypted at rest on macOS
    Given the app starts on macOS with no existing key ring
    When DataProtection initializes
    Then the generated key XML on disk is not the plaintext key
    And decryption requires access to the macOS Keychain entry the app created

  Scenario: Key-ring files are encrypted at rest on Linux
    Given the app starts on a Linux host with `libsecret` (or the chosen backing store) available
    When DataProtection initializes
    Then the generated key XML on disk is not the plaintext key
    And decryption requires access to the libsecret entry the app created

  Scenario: Windows behavior is unchanged
    Given the app starts on Windows
    When DataProtection initializes
    Then DPAPI continues to wrap the key ring exactly as it does today
    And no regression in existing Claude / Todoist token round-tripping

  Scenario: Startup warns when key ring sits on disk with loose permissions
    Given the backing-store approach is unavailable on the current host
    When the app starts
    Then the startup log emits a warning naming the key-ring directory and the permission bits
    And the README security note is linked in the warning message
```

---

## [MEP-040] Raise Test Coverage on the Offline Tools Projects

**Status:** Backlog
**Priority:** Low
**Depends on:** none

### Business Problem
The CI coverage gate is scoped to `MealsEnPlace.Api` (93.9%). The two offline
console utilities are outside that gate and are materially less covered:

| Project | Line coverage |
|---|---|
| `MealsEnPlace.Tools.Dedup` | 85.1% |
| `MealsEnPlace.Tools.Ingest` | 54.0% |

The scoping decision was deliberate — these tools are never deployed, and
holding them to the API's bar would either weaken that bar or block unrelated
dependency work on test debt. It is not an argument that the gap is fine. The
ingest tool writes directly to the recipe database and is the entry point for
the bulk Kaggle catalog, so a defect there corrupts the data every other
feature reads. That is the least pleasant place in the codebase to have half
the lines untested.

The number surfaced when `coverlet.collector` went 6.0.4 -> 10.0.1: the same
code measured 4518 coverable lines against 1900 before. The earlier figure was
not isolated, so treat the current measurement as the first trustworthy one
rather than as a regression.

### Acceptance Criteria
```gherkin
Feature: Offline Tools Test Coverage

  Scenario: Ingest tool reaches the API's coverage bar
    Given the MealsEnPlace.Tools.Ingest project
    When the test suite runs with coverage collection
    Then line coverage for that assembly is at least 90%

  Scenario: Dedup tool reaches the API's coverage bar
    Given the MealsEnPlace.Tools.Dedup project
    When the test suite runs with coverage collection
    Then line coverage for that assembly is at least 90%

  Scenario: The write path is covered against malformed input
    Given a Kaggle row with over-length strings, embedded NULs, or missing measures
    When the ingest write path processes it
    Then the row is truncated or rejected per the MEP-026 rules
    And no malformed value reaches the database

  Scenario: Both tools rejoin the gate once they clear the bar
    Given both tool assemblies are at or above 90%
    When coverlet.runsettings is reviewed
    Then the Include filter is widened to cover them
    And the CI gate enforces 90% across all three assemblies
```

---

## [MEP-041] Angular Frontend Test Infrastructure (Vitest)

**Status:** Done
**Priority:** High
**Depends on:** none

### Business Problem
The .NET side of the codebase has 598 unit tests and 18 integration tests behind
a CI coverage gate enforcing 90% line coverage. The Angular frontend has zero
executable tests and no configured test runner. There is literally no way to run
a spec file today. Every Angular component shipped so far -- inventory management,
meal plan board, recipe browser, shopping list, settings, container resolution
dialog -- is completely untested.

MEP-036 surfaced the gap by being the first story to produce a `.spec.ts` file
(`todoist-project-picker-dialog.component.spec.ts`), but that spec has never been
executed because no runner is installed. The project shows evidence of an
incomplete Vitest setup: `tsconfig.spec.json` already declares
`"types": ["vitest/globals"]` and includes `src/**/*.spec.ts`, but `package.json`
lists no test runner dependency (no vitest, no karma, no jest), and `angular.json`
has no `test` architect target. Someone chose a direction and the work stopped
partway.

Angular 22 dropped Karma support. The Frontend Engineer's assessment recommends
Vitest via `@analogjs/vitest-angular`: three devDependencies (`vitest`,
`@analogjs/vitest-angular`, `@vitest/coverage-v8`), a `vitest.config.ts` using
the Analog plugin with `environment: 'jsdom'` and the Analog setup file, and
either a `test` target in `angular.json` using the
`@analogjs/vitest-angular:test` builder or updating the npm script to
`vitest run`. The existing MEP-036 spec is already Vitest-native and requires no
changes once the runner is in place.

This is blocking infrastructure. Until it ships, no frontend test can execute,
and the testing asymmetry between backend and frontend will widen with every new
component.

### Acceptance Criteria
```gherkin
Feature: Angular Frontend Test Infrastructure

  Scenario: Test runner is installed and npm test executes specs
    Given the Angular project has vitest, @analogjs/vitest-angular, and @vitest/coverage-v8 as devDependencies
    And a vitest.config.ts exists using the Analog plugin with environment "jsdom"
    And angular.json has a test architect target or the npm test script invokes vitest
    When a developer runs "npm test" from the MealsEnPlace.Web directory
    Then vitest discovers and executes all *.spec.ts files under src/
    And the process exits with code 0 when all specs pass

  Scenario: Existing MEP-036 dialog spec runs and passes
    Given the todoist-project-picker-dialog.component.spec.ts file exists from MEP-036
    When vitest runs the full test suite
    Then the MEP-036 spec is discovered, executed, and passes
    And no changes to the spec file itself are required

  Scenario: Coverage collection works and reports a number
    Given @vitest/coverage-v8 is configured
    When a developer runs "npm test -- --coverage"
    Then a coverage report is generated for the Angular source files
    And the report shows a line-coverage percentage

  Scenario: Coverage gate participation decision is recorded
    Given the frontend test infrastructure is operational
    When the team reviews coverage results
    Then a decision is recorded on whether the Angular project joins a CI coverage gate and at what threshold
    And the decision follows the same pattern as MEP-040 (offline tools outside the gate until they clear a bar)
    And if the frontend stays outside the gate initially, the rationale and target threshold are documented
```

### Scope decisions made during implementation

**The runner is the first-party `@angular/build:unit-test` builder, not `@analogjs/vitest-angular`.**
The AC above named Analog because that was the best option known when the story was
written. It is the wrong one for this app: `@analogjs/vitest-angular` declares `zone.js`
as a hard peer dependency, and this application is zoneless — Angular 22's default, with
no `zone.js` installed and no `provideZoneChangeDetection` anywhere. Adopting Analog
would have meant reintroducing `zone.js` solely for tests, leaving the test-time change
detection model different from production.

`@angular/build` 22.1.4 already ships a `unit-test` builder with a Vitest runner that
supports zoneless natively. It needs the same count of devDependencies the AC
anticipated — `vitest`, `jsdom`, `@vitest/coverage-v8` — and no `vitest.config.ts`,
because the builder generates the Vitest configuration from the `test` target in
`angular.json`. The tradeoff accepted: the builder is flagged `[EXPERIMENTAL]` by the
Angular team. That is judged the smaller risk, since it is the path `ng new` scaffolds
and it tracks the framework version directly rather than lagging it.

**The frontend joined the CI coverage gate immediately at 90%, not after a ramp.**
This departs from the MEP-040 pattern the AC pointed at. MEP-040 keeps the offline tools
outside the gate because holding them to the bar would block unrelated dependency work on
test debt. The frontend is different: it is user-facing, actively developed, and its
untested surface was the reason this story exists. Deferring the gate would have shipped
the runner and left the asymmetry in place.

The threshold is enforced by `coverageThresholds.lines` on the `test` target in
`angular.json`, so it fails locally and in CI identically, with no shell check to drift.
The suite ships at 558 tests and 95.01% line coverage. Coverage excludes `main.ts`,
`environments/`, `app.config.ts`, the `*.routes.ts` files, and `core/models/` — bootstrap,
configuration, and type-only declarations, matching the exclusions the .NET gate already
applies.

**Two defects surfaced while writing the specs and were fixed here.**
- `AiAvailabilityService.refresh` and all three `PreferencesService` writes subscribed
  with only a `next` handler, so an API failure escaped as an uncaught error rather than
  leaving the last known state in place. They now handle errors the way
  `TodoistAvailabilityService` already did.
- `InventoryDialogComponent.ingredientNotResolved` was a `computed` reading a
  `FormControl`. A computed cannot track a `FormControl`, so it cached its first value and
  the "select an ingredient from the list" error never appeared. It now reads
  `ingredientQuery`, which is a real signal already kept in step by the input handlers.

**The MEP-043 recipe-browser spec was ported from Jasmine to Vitest.** It used
`jasmine.createSpyObj`, `.and.returnValue`, and `.calls.reset()`, none of which exist under
Vitest, and it was missing the router provider its `RouterLink` usage needs. The MEP-036
dialog spec was already Vitest-native and runs unchanged, as the AC required.

---

## [MEP-042] Migrate Todoist Integration from REST v2 to Unified API v1

**Status:** Done
**Priority:** High
**Depends on:** MEP-028 (shopping list push), MEP-029 (meal plan push), MEP-035 (token entry and Test Connection), MEP-036 (project quick-pick and history endpoint)

### Business Problem
The Todoist integration shipped under MEP-028, MEP-029, MEP-035, and MEP-036 targets
Todoist's REST API v2 (`/rest/v2/` paths). Todoist has deprecated that API surface and is
actively returning deprecation notices instead of results. The user discovered the breakage
by clicking "Test Connection" on the Settings page, but the problem is not confined to that
button: all six call sites across four files use `/rest/v2/` paths, which means shopping list
push (MEP-028), meal plan push (MEP-029), Test Connection (MEP-035), and the project
quick-pick name resolution (MEP-036) are all broken or on borrowed time. This is a live
outage of shipped functionality, not a future-proofing exercise.

The replacement is Todoist's Unified API v1, documented at
`https://developer.todoist.com/`, with a base path of
`https://api.todoist.com/api/v1/`. Authentication is unchanged (`Authorization: Bearer
{token}`), but two structural changes affect the implementation beyond a path swap:

1. **Response envelope change.** List endpoints (`GET /projects`, `GET /tasks`, etc.) no
   longer return a bare JSON array. They return a paginated envelope
   `{"results": [...], "next_cursor": "..." | null}`. Both `TodoistProjectClient` and
   `TodoistTestClient` currently deserialize `GET /projects` as a bare array and will fail
   to parse even after the path is corrected. Accounts with many projects require
   cursor-following rather than a single call.

2. **Object ID format change.** API v1 uses alphanumeric IDs (e.g., `69mF7QcCj9JmXxp8`);
   REST v2 used numeric IDs (e.g., `7246645180`). Every `ExternalTaskLink` row persisted by
   prior pushes stores REST v2-format IDs in both `ExternalTaskId` and `ExternalProjectId`.
   Against API v1 those stored IDs are the wrong format, which breaks two things:
   - **Push idempotency.** MEP-028 and MEP-029 update and close existing tasks by stored
     `ExternalTaskId`. Stale-format IDs mean updates fail or, worse, duplicate tasks get
     created on the next push.
   - **MEP-036 project quick-pick.** It matches stored `ExternalProjectId` values against
     the project list returned by Todoist to resolve display names. With mismatched ID
     formats, every previously-used project fails to resolve and degrades to a raw ID.

The implementation must make an explicit decision between two strategies for the stored-ID
problem and document the tradeoff:
- **(a) Migrate stored IDs** via the Todoist ID-mapping endpoint (reported to exist under
  `/api/v1/ids/`; the exact path and shape must be confirmed against the live docs at
  implementation time rather than assumed from this description). This preserves push
  history, idempotency, and MEP-036 quick-pick continuity, but adds complexity and a
  network dependency on the mapping endpoint.
- **(b) Accept a one-time reset** of `ExternalTaskLink` rows, which loses push history and
  the MEP-036 quick-pick project history. This is far simpler but means the next push after
  upgrade re-creates all tasks rather than updating them, which could duplicate tasks already
  in the user's Todoist. The user would need to manually close or delete the old tasks.

### Acceptance Criteria
```gherkin
Feature: Migrate Todoist Integration from REST v2 to Unified API v1

  Scenario: Base address and all call-site paths updated to API v1
    Given Program.cs configures the Todoist HttpClient with a base address
    And six call sites across TodoistClient.cs, TodoistProjectClient.cs, TodoistTestClient.cs, and ITodoistTestClient.cs reference /rest/v2/ paths
    When this story is implemented
    Then the base address points at the API v1 root (https://api.todoist.com/api/v1/)
    And every call site uses the corresponding /api/v1/ path
    And no /rest/v2/ path reference remains in any tracked file (verified via grep gate)
    And the ITodoistTestClient XML doc comment referencing the v2 path is updated

  Scenario: GET /projects consumers parse the paginated envelope
    Given Todoist API v1 GET /projects returns {"results": [...], "next_cursor": "..." | null}
    And TodoistProjectClient and TodoistTestClient both currently deserialize the response as a bare JSON array
    When this story is implemented
    Then both consumers deserialize the {"results", "next_cursor"} envelope
    And cursor-following is implemented so accounts with many projects return all results
    And the deserialization model is shared between the two consumers

  Scenario: Test Connection returns a real success or failure
    Given the user clicks "Test connection" on the Todoist section of the Settings page
    When the backend issues the API v1 equivalent of the projects call
    Then a valid token returns success with no deprecation notice
    And an invalid token returns the Todoist-reported error message
    And an unreachable Todoist API returns a clear network-error message

  Scenario: Task create works against API v1
    Given a shopping list or meal plan push creates new Todoist tasks
    When the push executes via POST /api/v1/tasks
    Then tasks are created in the configured project
    And the response is parsed to extract the new API v1-format task ID
    And the ExternalTaskLink row stores the API v1-format ID

  Scenario: Task update works against API v1
    Given an ExternalTaskLink row exists with an API v1-format ExternalTaskId
    And the corresponding shopping list item or meal plan slot has changed (ContentHash differs)
    When the push executes
    Then the existing Todoist task is updated via POST /api/v1/tasks/{id}
    And no duplicate task is created

  Scenario: Task close works against API v1
    Given an ExternalTaskLink row exists for an item that has been removed from the source
    When the push executes
    Then the Todoist task is closed via POST /api/v1/tasks/{id}/close
    And the ExternalTaskLink row is updated accordingly

  Scenario: Push idempotency is preserved end-to-end
    Given a shopping list has been pushed to Todoist via API v1
    And no items have changed since the last push
    When the user pushes again
    Then the push result reports zero created, zero updated, zero closed
    And no duplicate tasks appear in Todoist

  Scenario: Stored-ID migration strategy is decided and documented
    Given ExternalTaskLink rows from prior pushes store REST v2-format numeric IDs
    And API v1 uses alphanumeric IDs that do not match the stored values
    When the implementation begins
    Then the team selects either (a) migrating stored IDs via the Todoist ID-mapping endpoint or (b) resetting ExternalTaskLink rows
    And the decision is documented in the Implementation Notes section of this backlog item
    And if option (a) is chosen, the ID-mapping endpoint path and shape are confirmed against live Todoist docs before coding begins
    And if option (b) is chosen, the migration deletes or archives stale ExternalTaskLink rows and the user is informed that the next push will re-create tasks

  Scenario: MEP-036 project quick-pick resolves display names after migration
    Given the stored-ID strategy has been applied
    When the user opens the Todoist push dialog on the shopping list page or meal plan board
    Then GET /api/v1/settings/todoist/projects/history returns previously-used projects with resolved display names
    And the degraded path (200 with namesResolved: false) is preserved when Todoist is unreachable

  Scenario: Existing tests are updated for API v1 behavior
    Given unit tests exist for TodoistClient, TodoistProjectClient, TodoistTestClient, and the push targets
    When this story is implemented
    Then all test assertions reference the API v1 paths and response shapes
    And tests for the paginated envelope and cursor-following are added
    And the MEP-036 degraded-path test (200 with namesResolved: false) continues to pass
    And the full test suite passes with no regressions
```

### Implementation Notes

**Stored-ID migration decision:** The deployment database was checked at implementation
time (`SELECT COUNT(*) FROM "ExternalTaskLinks"` returned 0). No push history existed,
so no REST v2-format IDs were persisted anywhere. The ID-mapping-endpoint path (option a)
was therefore unnecessary and was not implemented. Option b (reset) was effectively a no-op
since there was nothing to reset. Any future deployment that has v2-era rows in
`ExternalTaskLink` would need to run the Todoist ID-mapping endpoint (`/api/v1/ids/`) to
translate stored numeric IDs to alphanumeric API v1 IDs before the next push — that pass
is not present in this codebase and would need to be added as a one-time data migration if
the need arises.

**BaseAddress/path convention:** `BaseAddress` remains `https://api.todoist.com` (origin
only, no path component). All request URIs use full absolute paths with a leading slash,
e.g. `/api/v1/projects`. This avoids the HttpClient relative-URI trap where a leading slash
would discard any path already on `BaseAddress`.

**Cursor-following safety:** `TodoistProjectClient.GetProjectsAsync` follows the
`next_cursor` field across pages. Two safety mechanisms guard against infinite loops: (1) a
hard cap of 50 pages, and (2) an equality check — if the returned `next_cursor` equals the
cursor used for the current request, iteration stops. A network or non-2xx failure on any
page returns `Succeeded = false` immediately rather than returning a partial list silently.

**Shared envelope type:** `TodoistProjectPageEnvelope` and `TodoistProjectEnvelopeItem` are
defined in `TodoistProjectPageEnvelope.cs` (internal, Todoist namespace) and used by
`TodoistProjectClient`. `TodoistTestClient.PingAsync` checks only the HTTP status code and
does not parse the body, so it does not consume the envelope type — but the type is
available in the shared namespace if that ever changes.

---

## [MEP-043] Recipe List Endpoint Pagination and Query Optimization

**Status:** Done
**Priority:** High
**Depends on:** MEP-026 (bulk ingest created the data volume that makes the unbounded query fatal)

### Business Problem
The recipes page is completely broken. Opening it in the browser shows "Failed to load
recipes. Please try again." and the API returns HTTP 500 after approximately 32 seconds.
The root cause is that `GET /api/v1/recipes` loads the entire Recipes table --
1,643,098 rows with 13,635,157 joined RecipeIngredient rows -- into memory in a single
unbounded query. The endpoint accepts no paging parameters; its implementation calls
`ToListAsync` on a query with two `Include`/`ThenInclude` collection navigations (DietaryTags
and RecipeIngredients with CanonicalIngredient), which EF Core executes as a single SQL
statement containing two left-joined collection subqueries. This is the exact pattern EF
Core's `MultipleCollectionIncludeWarning` exists to flag: it produces a cartesian explosion
where every combination of DietaryTag and RecipeIngredient rows is materialized. The
resulting SQL contains no LIMIT and no OFFSET, so Postgres attempts to build the full result
set, exceeds the 30-second command timeout, and cancels the statement
(`Npgsql.PostgresException 57014`), which the API surfaces as a 500.

The endpoint worked before MEP-026 because the recipe catalog was on the order of hundreds
of rows (TheMealDB's roughly 600 recipes). The Kaggle ingest grew the data by five orders
of magnitude and exposed an always-unbounded query that was already technically incorrect
(the cartesian explosion existed before, it just completed within the timeout at small
scale).

This is a live defect on a primary user-facing page with no workaround -- the user cannot
browse, search, or interact with their recipe library at all. There is no client-side
fallback because the endpoint returns zero usable data.

### Acceptance Criteria
```gherkin
Feature: Recipe List Endpoint Pagination and Query Optimization

  Scenario: Recipe list endpoint accepts pagination parameters
    Given the recipe catalog contains over 1,600,000 rows
    When I call GET /api/v1/recipes with page=1 and pageSize=25
    Then the response contains at most 25 recipe items
    And the response includes totalCount metadata reflecting the full catalog size
    And the response includes the current page number and page size

  Scenario: Default pagination when no parameters are supplied
    Given the recipe catalog contains over 1,600,000 rows
    When I call GET /api/v1/recipes with no pagination parameters
    Then the response uses a sensible default page size (e.g. 25)
    And the response returns only the first page of results
    And the response is not unbounded

  Scenario: Maximum page size is enforced
    Given a caller requests GET /api/v1/recipes with pageSize=10000
    When the server processes the request
    Then the page size is clamped to a documented maximum (e.g. 100)
    And the response contains at most that maximum number of items

  Scenario: Page of recipes returns well within the command timeout
    Given the recipe catalog contains over 1,600,000 rows
    When I call GET /api/v1/recipes with page=1 and pageSize=25
    Then the response returns in under 2 seconds
    And no Npgsql command timeout or cancellation exception occurs

  Scenario: Query uses projection instead of Include/ThenInclude
    Given the endpoint previously used Include(DietaryTags) and Include(RecipeIngredients).ThenInclude(CanonicalIngredient)
    When the implementation is updated
    Then the query projects directly to RecipeListItemDto in the database via Select
    And the emitted SQL does not produce a cartesian product across collection navigations
    And the EF Core MultipleCollectionIncludeWarning is no longer triggered

  Scenario: RecipeListItemDto contains the required summary fields
    Given the list endpoint projects to RecipeListItemDto
    When the projection runs
    Then each item includes TotalIngredients as a count of the recipe's ingredients
    And each item includes UnresolvedCount as a count of unresolved container references
    And each item includes DietaryTags as a list of tag names
    And the implementation evaluates whether IngredientNames belongs on the list DTO or should be deferred to the detail endpoint to keep the list query lean

  Scenario: AsSplitQuery is used where collection loads remain
    Given any query path that still loads multiple collection navigations
    When the query executes
    Then AsSplitQuery is applied to prevent cartesian explosion
    And each collection loads in a separate SQL statement

  Scenario: Database index supports ORDER BY Title at scale
    Given the recipe catalog contains over 1,600,000 rows
    And the list endpoint orders results by Title
    When the query executes
    Then a database index on Recipes.Title supports the sort without a full table scan
    And the total-count query is supported by an efficient path (index-only count or similar)

  Scenario: Shared pagination helper is introduced or a decision is documented
    Given no shared pagination helper currently exists under Common/
    And other list endpoints will need the same pagination treatment
    When this story is implemented
    Then either a shared pagination model (page, pageSize, totalCount response wrapper) is introduced under Common/
    Or the decision to defer the shared helper is documented with a rationale

  Scenario: Angular recipe browser uses server-side pagination
    Given the frontend previously expected the full recipe list in a single response
    When the recipe browser component is updated
    Then it sends page and pageSize query parameters to the API
    And it renders pagination controls (next, previous, page indicator)
    And the "Failed to load recipes" error no longer appears

  Scenario: Recipes page renders successfully against the bulk catalog
    Given the recipe catalog contains over 1,600,000 rows
    When I open the recipes page in the browser
    Then the page loads and displays the first page of recipes
    And I can navigate to subsequent pages
    And no HTTP 500 or timeout error occurs

  Scenario: Sibling list endpoints are audited for the same unbounded pattern
    Given MEP-026 grew the data volume across multiple tables
    When this story is implemented
    Then all other list endpoints under Recipes/ are checked for unbounded queries
    And any other list endpoint in the API that predates MEP-026 is checked for the same pattern
    And any endpoint found to be unbounded is either fixed in this story or a follow-on backlog item is filed
```

### Implementation Notes

**IngredientNames removal (deliberate, verified-unused):** `RecipeListItemDto.IngredientNames`
was removed. The entire Angular app was grepped for `ingredientNames`; the string appears only
in the TypeScript model declaration (`core/models/recipe.models.ts`) and is not referenced by
any template, component, or service. It was the sole reason the previous query joined all 13.6M
RecipeIngredient rows, which caused the Postgres 57014 command-timeout 500.

**Page size max — 100:** A page of 100 summary rows (no ingredient data, all projected as
scalars) is fast at the database level. Larger values risk re-approaching the command timeout;
100 is more items than any practical recipe-browser page would display. Default is 25.

**Clamping, not 400:** Out-of-range page and pageSize values are silently clamped (page < 1 → 1;
pageSize > 100 → 100; pageSize < 1 → 1). Client errors for boundary values are surprising and
unhelpful; clamping produces a valid, predictable result.

**Shared `PagedResult<T>` introduced in `Common/`:** Introduced now rather than deferred.
Other endpoints will need the same pagination treatment (see sibling audit below), and the type
is small enough that the upfront cost is trivial. Keeping pagination local would mean
duplicating the response envelope across every future paged endpoint.

**`IsFullyResolved` projection:** The C# computed property cannot be translated to SQL directly.
It is expressed in the EF Core projection as:
`r.RecipeIngredients.Any() && r.RecipeIngredients.All(ri => ri.IsContainerResolved)`.
EF Core 10 translates this to two SQL EXISTS subqueries — no client evaluation.

**Database index:** `IX_Recipes_Title` (B-tree) on `Recipes.Title` added in migration
`20260831013330_AddRecipesTitleIndex`. Up creates the index; Down drops it.
The index creation on the live 1.6M-row table took approximately 4.3 seconds (one-time migration cost).

**Measured endpoint latency:** First request after JIT warm-up: 313ms. Steady state (second
request): 233ms. Both well under the 2-second requirement. Deep-offset pagination (page 10000,
SKIP 249,975 rows) degrades to approximately 10 seconds — that is a known PostgreSQL large-OFFSET
limitation, not introduced by this change. Keyset (cursor-based) pagination is the remedy and
is filed as MEP-044.

**Sibling endpoint audit (unbounded queries):**

| Endpoint | Service/Method | Unbounded? | Risk |
|---|---|---|---|
| `GET /api/v1/recipes/unresolved` | `ContainerResolutionService.GetUnresolvedRecipesAsync` | Yes — no LIMIT | Medium: filtered to recipes with unresolved ingredients; still could be large post-ingest |
| `GET /api/v1/recipes/unresolved-groups` | `ContainerResolutionService.GetUnresolvedGroupsAsync` | Yes — no LIMIT | Medium: grouped/aggregated query on RecipeIngredients filtered by IsContainerResolved=false |
| `GET /api/v1/inventory` | `InventoryRepository.GetAllAsync` | Yes — no LIMIT | Low: per-user inventory; no bulk ingest path; realistically bounded to hundreds of rows |
| `GET /api/v1/inventory/ingredients` | `ReferenceDataController.ListIngredients` | Yes — no LIMIT | High: CanonicalIngredients table grows with bulk ingest dedup; could reach hundreds of thousands |
| `GET /api/v1/inventory/units` | `ReferenceDataController.ListUnits` | Yes — no LIMIT | None: seed data only, ~20 rows, never grows |
| `GET /api/v1/seasonality` | `SeasonalProduceService` | Yes — no LIMIT | None: seed data only, ~12 rows |

Follow-on items filed: MEP-044 (keyset pagination for deep recipe pages), MEP-045 (paginate
`/inventory/ingredients`).

---

## [MEP-044] Keyset Pagination for Deep Recipe Pages

**Status:** Backlog
**Priority:** Medium
**Depends on:** MEP-043 (introduced the OFFSET-based pagination that this story replaces)

### Business Problem
MEP-043 replaced the unbounded recipe list query with OFFSET-based pagination. Page 1
now returns in approximately 120ms (down from a 32-second timeout), and the endpoint is
functional for normal browsing. However, OFFSET pagination degrades linearly with depth
because PostgreSQL must skip all preceding rows before returning the requested page.
Against the live 1,643,098-row catalog, page 1000 was measured at 19.9 seconds, and pages
beyond approximately 1500 will exceed the 30-second Postgres command timeout and return
HTTP 500 again. With 16,431 total pages at the default page size of 100, more than 90% of
the catalog is unreachable via direct paging.

A guard rail already shipped in MEP-043's frontend: the Angular paginator offers only
previous/next navigation -- no first/last buttons, no arbitrary page jumps -- so users
cannot navigate into the failing range. This prevents the user from encountering the error
today, but it is a UI constraint masking a data-access limitation, not a fix. The catalog
remains truncated in practice.

Keyset (cursor-based) pagination is the remedy. Instead of `OFFSET @skip`, the query uses
a composite cursor on the existing sort key:
`WHERE (Title, Id) > (@lastTitle, @lastId) ORDER BY Title, Id LIMIT @pageSize`. This
executes in constant time at any depth because the B-tree index on `(Title, Id)` seeks
directly to the cursor position. The `IX_Recipes_Title` index created in MEP-043 already
covers the `Title` column; a composite index on `(Title, Id)` may be needed depending on
query plan analysis.

**Tradeoffs:** Keyset pagination cannot jump to an arbitrary page number -- it requires
the cursor from the previous page to fetch the next one. This pairs naturally with the
previous/next-only UI MEP-043 already shipped, so no UI regression occurs. The API
contract changes: the response returns a cursor string (opaque to the client) instead of
a page number, and the request accepts a cursor parameter instead of a page parameter.
The `PagedResult<T>` helper from MEP-043 under `Common/` will need to be extended or
supplemented with a cursor-based variant. Existing consumers of the page-number-based
contract (the Angular recipe browser) must be updated.

### Acceptance Criteria
```gherkin
Feature: Keyset Pagination for Deep Recipe Pages

  Scenario: Recipe list endpoint accepts a cursor parameter
    Given the recipe catalog contains over 1,600,000 rows
    When I call GET /api/v1/recipes with no cursor parameter
    Then the response returns the first page of results ordered by Title, Id
    And the response includes a nextCursor value for fetching the next page
    And the response includes totalCount metadata

  Scenario: Fetching the next page via cursor
    Given I have a nextCursor value from a previous recipe list response
    When I call GET /api/v1/recipes with that cursor value
    Then the response returns the next page of results immediately following the cursor position
    And no results from the previous page are repeated
    And no results are skipped

  Scenario: Deep pages return in constant time
    Given the recipe catalog contains over 1,600,000 rows
    When I fetch page 1000 using sequential cursor-based navigation
    Then the response returns in under 2 seconds
    And no Npgsql command timeout or cancellation exception occurs

  Scenario: Last page indicates no more results
    Given I am on the final page of the recipe catalog
    When I fetch that page via cursor
    Then the response includes an empty or null nextCursor value
    And the response contains fewer items than the requested page size or zero items

  Scenario: Angular recipe browser uses cursor-based navigation
    Given the frontend previously sent page number parameters to the API
    When the recipe browser component is updated for cursor-based pagination
    Then it sends cursor parameters instead of page numbers
    And previous/next navigation continues to work correctly
    And no arbitrary page-jump controls are offered
```

---

## [MEP-045] Paginate GET /api/v1/inventory/ingredients

**Status:** Superseded by MEP-048
**Priority:** Medium
**Depends on:** MEP-043 (introduced the `PagedResult<T>` helper this story reuses)

### Supersession Note
MEP-048 replaces this item. The core goal -- eliminating the unbounded full-table dump
from `GET /api/v1/referencedata/ingredients` -- is achieved by a bounded typed-search
endpoint (search + limit parameters, blank search returns empty) rather than offset
pagination. Offset pagination is the wrong shape for an autocomplete: the user never pages
through canonical ingredients; they type a name fragment and pick from a short result list.
Additionally, this item cited the wrong route (`/api/v1/inventory/ingredients`; the real
route is `/api/v1/referencedata/ingredients`) and a stale row count (146,584; the table
holds 120,505 rows as of 2026-09-10).

### Business Problem
MEP-043's sibling-endpoint audit identified `GET /api/v1/inventory/ingredients` as the
highest-risk unbounded endpoint remaining in the API. This endpoint calls
`ReferenceDataController.ListIngredients`, which returns every row in the
`CanonicalIngredients` table with no LIMIT clause. After the MEP-026 bulk Kaggle ingest
and the MEP-038 morphological deduplication pass, that table holds approximately 146,584
rows. Unlike seed-data endpoints (units of measure at ~20 rows, seasonality at ~12 rows),
the canonical ingredients table grows directly with the recipe catalog and will grow
further with any future ingest.

The endpoint is not as catastrophic as the pre-MEP-043 recipe list was -- 146K simple
rows versus 1.6M rows with joined collections -- but it is on the same trajectory. At the
current row count, the response payload is already several megabytes of JSON, which
degrades frontend performance and increases memory pressure on both the server and the
browser. As the catalog grows, this endpoint will eventually hit the same command timeout
that took down the recipe list.

The fix is the same pattern MEP-043 established: server-side pagination with
database-side projection, reusing the `PagedResult<T>` helper introduced under `Common/`.

**Remaining audit findings (for the record):** The following unbounded endpoints were
identified in MEP-043's audit but do not warrant dedicated backlog items at this time:

- `GET /api/v1/recipes/unresolved` and `GET /api/v1/recipes/unresolved-groups` -- medium
  risk. These are filtered to recipes with unresolved container references, which bounds
  the result set in practice. They could still be large immediately after a bulk ingest
  before the user resolves containers. Monitor and paginate if the row count becomes
  problematic.
- `GET /api/v1/inventory` -- low risk. Returns per-user inventory items with no bulk
  ingest path. Realistically bounded to hundreds of rows. No action needed unless usage
  patterns change.
- `GET /api/v1/inventory/units` -- no risk. Seed data only, approximately 20 rows, never
  grows.
- `GET /api/v1/seasonality` -- no risk. Seed data only, approximately 12 rows, never
  grows.

### Acceptance Criteria
```gherkin
Feature: Paginate Canonical Ingredients Endpoint

  Scenario: Ingredients endpoint accepts pagination parameters
    Given the CanonicalIngredients table contains over 146,000 rows
    When I call GET /api/v1/inventory/ingredients with page=1 and pageSize=25
    Then the response contains at most 25 ingredient items
    And the response includes totalCount metadata reflecting the full table size
    And the response includes the current page number and page size

  Scenario: Default pagination when no parameters are supplied
    Given the CanonicalIngredients table contains over 146,000 rows
    When I call GET /api/v1/inventory/ingredients with no pagination parameters
    Then the response uses a default page size
    And the response returns only the first page of results
    And the response is not unbounded

  Scenario: Maximum page size is enforced
    Given a caller requests GET /api/v1/inventory/ingredients with pageSize=10000
    When the server processes the request
    Then the page size is clamped to the documented maximum
    And the response contains at most that maximum number of items

  Scenario: Response uses database-side projection
    Given the endpoint queries the CanonicalIngredients table
    When the query executes
    Then the SQL includes a LIMIT and OFFSET clause
    And the projection selects only the fields needed by the response DTO
    And the query does not load the full table into memory

  Scenario: PagedResult helper from Common is reused
    Given the PagedResult<T> helper was introduced in MEP-043 under Common/
    When this endpoint is paginated
    Then it uses the same PagedResult<T> response envelope
    And the response shape is consistent with GET /api/v1/recipes

  Scenario: Existing consumers continue to function
    Given frontend components or other endpoints consume the ingredients list
    When the endpoint is paginated
    Then existing consumers are updated to pass pagination parameters
    And no regression occurs in ingredient-dependent features (recipe matching, inventory add/edit)
```

---

## [MEP-046] Recipe Search and Filtering

**Status:** Done
**Priority:** High
**Depends on:** MEP-043 (pagination infrastructure the search results will page through)

### Business Problem
The recipe catalog contains 1,643,098 entries. MEP-043 made the catalog loadable by
adding pagination, but loadable is not the same as usable. Nobody pages through 16,431
pages to find dinner. The Angular recipe browser already has a dietary-tag filter chip row
and a search icon in the toolbar, but neither is backed by server-side query logic -- the
search icon is non-functional and the dietary-tag filter operates only on the current page
of results, not the full catalog.

Without server-side search, the catalog is effectively a very large, alphabetically sorted
list that the user can only browse sequentially. The user cannot answer the basic question
"do I have a recipe for chicken tikka masala?" without paging through hundreds of pages in
the T section. This makes the 1.6M-recipe catalog a liability rather than an asset -- the
data is there, but there is no way to find anything in it.

This story adds real server-side search: at minimum, title substring matching so the user
can type a recipe name and find it; ideally, ingredient matching so the user can search by
what they have on hand (e.g., "chicken thigh" returns recipes containing that ingredient).
Combined with the existing dietary-tag filter, this gives the user a practical way to
narrow the catalog to a manageable result set that pagination can handle.

**Technical approach:** At 1.6M rows, naive `LIKE '%term%'` queries will perform full
table scans and exceed the command timeout. PostgreSQL full-text search
(`tsvector`/`tsquery`) or a trigram index (`pg_trgm` extension with GIN/GiST index)
is the likely mechanism. Full-text search is better for natural-language queries and
relevance ranking; `pg_trgm` is better for substring and fuzzy matching. The choice
should be evaluated during implementation, but either approach requires an explicit EF
Core migration to create the index. The `pg_trgm` extension must be enabled via
`CREATE EXTENSION IF NOT EXISTS pg_trgm` in the migration if that path is chosen.

**Interaction with MEP-044:** If search narrows results to tens or hundreds of matches,
deep pagination ceases to be a problem for searched result sets. Keyset pagination
(MEP-044) remains relevant for unfiltered browsing of the full catalog, but in practice,
search may reduce the urgency of that work by ensuring most user interactions hit small
result sets.

### Acceptance Criteria
```gherkin
Feature: Recipe Search and Filtering

  Scenario: Search recipes by title
    Given the recipe catalog contains over 1,600,000 rows
    When I call GET /api/v1/recipes with a search query "tikka masala"
    Then the response contains only recipes whose titles match the search term
    And the response returns in under 2 seconds
    And the results are paginated using the existing pagination infrastructure

  Scenario: Search is case-insensitive
    Given recipes with titles "Chicken Tikka Masala" and "chicken tikka masala" exist
    When I search for "TIKKA MASALA"
    Then both recipes appear in the results

  Scenario: Search by ingredient name
    Given recipes exist that contain the ingredient "chicken thigh"
    When I search with an ingredient filter for "chicken thigh"
    Then the response contains recipes that include that ingredient
    And recipes without that ingredient are excluded

  Scenario: Search combines with dietary-tag filter
    Given recipes exist with various dietary tags and titles
    When I search for "pasta" with a dietary tag filter of "Vegetarian"
    Then the response contains only vegetarian recipes whose titles match "pasta"
    And the filters are applied server-side, not client-side

  Scenario: Empty search returns unfiltered paginated results
    Given the recipe catalog contains over 1,600,000 rows
    When I call GET /api/v1/recipes with no search query and no filters
    Then the response returns the standard paginated recipe list
    And behavior is identical to the existing MEP-043 pagination

  Scenario: Search with no matches returns an empty page
    Given no recipe title or ingredient matches "xyznonexistent123"
    When I search for "xyznonexistent123"
    Then the response contains zero items
    And totalCount is 0
    And no error occurs

  Scenario: Search is backed by a database index
    Given the recipe catalog contains over 1,600,000 rows
    When a search query executes
    Then the query uses a full-text search index or trigram index rather than a sequential scan
    And the index is created via an explicit EF Core migration

  Scenario: Angular recipe browser integrates search
    Given the recipe browser has an existing search icon in the toolbar
    When the user types a search term and submits
    Then the browser sends the search query as a parameter to the API
    And the results update to show only matching recipes
    And pagination resets to page 1 of the filtered results
```

---

## [MEP-047] Close Angular C4 Component Model Drift (PWA and Offline Units)

**Status:** Backlog
**Priority:** Low
**Depends on:** none

### Business Problem
The Angular component diagram (`docs/c4/component-web.puml`) currently declares 24
Component entries, but four real units of the running application are absent:
`NetworkStatusService`, `InstallPromptService`, `PushNotificationService`, and
`OfflineBannerComponent`. These units ship in the build, are exercised by the test suite
(MEP-041), and have relationships to the app shell and to each other, yet they do not appear
in the architecture model.

An inaccurate component diagram has a real, if undramatic, cost. When a developer consults
the diagram to understand the app shell's dependency graph -- for example, before refactoring
the toolbar or changing service-worker registration -- the missing nodes create a false
picture of what the shell actually depends on. The developer either discovers the gap during
implementation (wasted orientation time) or, worse, does not discover it and makes a change
that conflicts with an undocumented relationship. The cost compounds over time: each new
contributor who trusts the diagram inherits the same blind spot.

Before adding nodes, however, the team must decide whether the three PWA services
(`NetworkStatusService`, `InstallPromptService`, `PushNotificationService`) belong in a C4
component diagram at all. These are infrastructure plumbing rather than domain features, and
a reasonable architectural position is that they sit below the abstraction level the diagram
is intended to capture. If the team reaches that conclusion, recording the decision
explicitly (e.g., as a comment block in the PlantUML source) closes this item just as
validly as adding the nodes would. The `OfflineBannerComponent` is a visible UI element
rendered by the app shell and should be modelled regardless of the PWA-service decision.

### Acceptance Criteria
```gherkin
Feature: Angular C4 Component Model Accuracy

  Scenario: Decide whether PWA services belong in the component model
    Given the web component diagram omits NetworkStatusService, InstallPromptService, and PushNotificationService
    And these services are infrastructure plumbing rather than domain features
    When the team evaluates their fit for a C4 component-level diagram
    Then the decision is recorded explicitly in the PlantUML source as either new Component entries or a comment block explaining why they are intentionally excluded
    And future contributors can find the rationale without re-investigating

  Scenario: Add OfflineBannerComponent to the diagram
    Given OfflineBannerComponent is a visible UI element rendered by the app shell
    And it does not currently appear in the web component diagram
    When the diagram is updated
    Then OfflineBannerComponent appears as a Component entry
    And a relationship shows the App Component (appShell) rendering the OfflineBannerComponent
    And a relationship shows OfflineBannerComponent reading NetworkStatusService (if that service was included) or an annotation notes the dependency on an excluded infrastructure service

  Scenario: Add PWA services to the diagram if the decision is to include them
    Given the team decided to include PWA services in the component model
    When the diagram is updated
    Then NetworkStatusService, InstallPromptService, and PushNotificationService each appear as Component entries
    And a relationship shows the App Component using InstallPromptService for the toolbar install button
    And a relationship shows PushNotificationService depending on Angular SwPush as an external infrastructure dependency
    And the total Component count increases to 28

  Scenario: Record exclusion rationale if the decision is to omit PWA services
    Given the team decided that PWA services are below the diagram's abstraction level
    When the diagram is updated
    Then a comment block in the PlantUML source lists the excluded services by name
    And the comment explains the rationale for exclusion
    And OfflineBannerComponent is still added per the previous scenario
    And the total Component count increases to 25

  Scenario: Regenerate C4 PNGs locally and commit them
    Given the PlantUML source has been updated
    When the developer runs ./scripts/render-c4.sh
    Then the script renders updated PNGs via the Docker-hosted PlantUML renderer
    And the updated PNGs are committed alongside the PlantUML source changes
    And no CI workflow is expected to render them (rendering is local per the current process)
```

---

## [MEP-048] Server-Side Ingredient Search for the Inventory Dialog Autocomplete

**Status:** Done
**Priority:** High
**Depends on:** none

### Business Problem
The "Add Item" inventory dialog is effectively unusable. When the dialog opens, it calls
`GET /api/v1/referencedata/ingredients`, which returns every row in the
`CanonicalIngredients` table -- currently 120,505 rows, roughly 15 MB of JSON -- with no
LIMIT clause. The download alone is slow, but the real damage happens in the browser: the
autocomplete's `filteredIngredients` computed returns the full list when the query string
is empty, so focusing the Ingredient field causes Angular Material to render approximately
120,000 `mat-option` elements. Every keystroke then runs an unthrottled, undebounced
substring scan over all 120,505 names and re-renders the matching options. The result is
seconds-long freezes on every keypress, making it impossible to search for an ingredient
at a normal typing speed. A secondary issue is that the units request is chained
sequentially after the ingredients request instead of running in parallel, adding
unnecessary latency even before the autocomplete problem kicks in.

This item supersedes MEP-045, which proposed offset pagination for the same endpoint.
Offset pagination is the wrong shape for an autocomplete -- the user never pages through
canonical ingredients; they type a name fragment and pick from a short result list.

The inventory dialog is not the only consumer. The recipe create page
(`recipe-create.component.ts`) renders a `mat-select` per ingredient row over the same
full 120,505-row list, producing the same performance collapse. Because the new endpoint
returns an empty array for a blank search, that select would become empty if left
untouched. The fix is a shared standalone `IngredientAutocompleteComponent` (in
`src/app/shared/ingredient-autocomplete/`) implementing `ControlValueAccessor` with an
optional `allowCreate` input. The inventory dialog uses it with create enabled; the recipe
create page replaces its per-row `mat-select` with the same component, create disabled.
Debounce, minimum-length gating, and stale-request cancellation live in the shared
component -- one implementation, two consumers.

Follow-on candidate (out of scope here): many Kaggle-ingested canonical ingredient names
are low-quality junk strings ("a crowd", "type fruit", "bottles wegmans chili sauce") that
clutter search results and should be cleaned up in a separate data-quality pass. MEP-049
addresses the root cause -- noisy NER tokens entering CanonicalIngredients uncleaned at
ingest time -- and requires a full re-ingest to replace the affected rows.

### Scope decisions made during implementation

**Search results are ranked by a stored recipe-reference count, not a live correlated
COUNT.** The initial ordering -- prefix match first, then name ascending -- surfaced junk
at the top of results: "apple", "apple [", "apple.", "apple/", "apple add", because
punctuation sorts before letters. Ranking by how many recipes reference each ingredient
pushes well-known ingredients to the top, but a live `COUNT` correlated against the
13,635,157-row `RecipeIngredients` table was measured at 2,900 ms for the search term
"ch" (20,697 candidates) versus 104 ms without it. The count is therefore stored.

A new column `CanonicalIngredients.RecipeReferenceCount` (`int`, `NOT NULL`, default 0)
is added by an explicit EF Core migration that also backfills the value from
`RecipeIngredients` in a single `UPDATE ... FROM` statement. The migration must be applied
manually with `dotnet ef database update` before testing; it is not auto-applied at
startup.

Search ordering is: prefix match first, then `RecipeReferenceCount` descending, then
`Name` ascending, then `Take(limit)`. This means "pineapple" (67,693 references) still
ranks below "apple" (38,510 references) for the search term "apple" because prefix match
wins the first tiebreaker.

The count is incremented by `RecipeImportService` when a recipe is created through the
API (by the number of `RecipeIngredient` rows per canonical ingredient), and recomputed
once at the end of a Kaggle ingest run by `MealsEnPlace.Tools.Ingest`. There is no recipe
delete endpoint today; if one is added it must decrement the count.

**Verification.** On a clone of the user's database (120,506 canonical ingredients,
13,635,157 recipe ingredients) the migration applied cleanly and backfilled 118,072 rows.
The ranked search runs in 92 ms for "ch", 86 ms for "sa", and 92 ms for "apple" (versus
2,900 ms with the earlier correlated-count approach). The term "apple" now returns apple,
apple cider vinegar, applesauce, apple juice, apple cider as the first five results.
API suite: 646 unit + 18 integration tests pass, 90.8% line coverage. Angular suite: 585
tests pass, 94.6% line coverage. A defect QA caught before close: a failed search request
put the `rxResource` into an error state that threw in the template; the stream now
catches errors and degrades to an empty result set. Users must run
`dotnet ef database update` to apply the new migration before using this feature.

### Acceptance Criteria
```gherkin
Feature: Server-Side Ingredient Search for Inventory Dialog Autocomplete

  Scenario: Bounded search replaces unbounded list
    Given the CanonicalIngredients table contains 120,505 rows
    When I call GET /api/v1/referencedata/ingredients with search="chick" and limit=20
    Then the response contains at most 20 ingredient items
    And the response is a plain array of CanonicalIngredientDto (no PagedResult envelope)
    And the SQL query includes a LIMIT clause and never loads the full table

  Scenario: Blank or whitespace search returns an empty list
    Given the CanonicalIngredients table contains 120,505 rows
    When I call GET /api/v1/referencedata/ingredients with search="" or search="   "
    Then the response is an empty array with HTTP 200
    And no database query executes against the CanonicalIngredients table

  Scenario: Limit parameter is clamped to a safe range
    Given a caller requests GET /api/v1/referencedata/ingredients with search="rice" and limit=500
    When the server processes the request
    Then the limit is clamped to 50
    And the response contains at most 50 items

  Scenario: Default limit is applied when limit is omitted
    Given a caller requests GET /api/v1/referencedata/ingredients with search="butter" and no limit parameter
    When the server processes the request
    Then the server uses a default limit of 20
    And the response contains at most 20 items

  Scenario: Search is case-insensitive
    Given a canonical ingredient named "Chicken Breast" exists
    When I search with search="chicken breast"
    Then "Chicken Breast" appears in the results

  Scenario: Prefix matches are ranked before substring matches
    Given canonical ingredients "Garlic" and "Roasted Garlic Hummus" exist
    When I search with search="garlic"
    Then "Garlic" appears before "Roasted Garlic Hummus" in the results

  Scenario: Results use database-side DTO projection
    Given the endpoint queries the CanonicalIngredients table
    When the query executes
    Then the SQL projects only the fields needed by CanonicalIngredientDto
    And no full entity materialization occurs

  Scenario: Swagger documentation reflects the new parameters
    Given the OpenAPI spec is generated by Swashbuckle
    When a developer inspects the spec for GET /api/v1/referencedata/ingredients
    Then the search (string) and limit (integer, default 20, range 1-50) query parameters are documented
    And the endpoint description explains that blank search returns an empty list

  Scenario: POST endpoint and units endpoint are unchanged
    Given POST /api/v1/referencedata/ingredients exists for creating new ingredients
    And GET /api/v1/referencedata/units exists for listing units of measure
    When the search parameters are added to the GET ingredients endpoint
    Then the POST endpoint behavior is unaffected
    And the GET units endpoint behavior is unaffected

  Scenario: Angular dialog debounces ingredient search input
    Given the user opens the "Add Item" inventory dialog
    When the user types "chi" into the ingredient autocomplete field
    Then the dialog waits 250 ms after the last keystroke before sending a search request
    And typing additional characters within the 250 ms window resets the debounce timer
    And no request is sent until the debounced input stabilizes

  Scenario: Minimum character threshold prevents trivial searches
    Given the user opens the "Add Item" inventory dialog
    When the user types a single character "c" into the ingredient autocomplete field
    Then no search request is sent to the API
    When the user types a second character making the input "ch"
    Then a search request is sent after the debounce period

  Scenario: Stale in-flight requests are cancelled
    Given the user types "chi" and a search request is in flight
    When the user continues typing to "chic" before the first request returns
    Then the first request is cancelled
    And only the result of the "chic" search is displayed

  Scenario: Loading indicator appears during search
    Given the user has typed "chick" and the debounce period has elapsed
    When the search request is in flight
    Then a loading indicator appears in the autocomplete dropdown
    When the results arrive
    Then the loading indicator is replaced by the matching ingredient options

  Scenario: Create-new option derives from debounced query and search results
    Given the user types "Dragon Fruit" into the ingredient autocomplete
    And the search returns no exact match
    When the autocomplete results are displayed
    Then a "Create Dragon Fruit" option appears at the end of the list
    And the option text reflects the debounced query, not a stale value

  Scenario: Edit mode pre-fills ingredient name and selected ID
    Given I am editing an existing inventory item with ingredient "Olive Oil" (ID 42)
    When the edit dialog opens
    Then the ingredient autocomplete field displays "Olive Oil"
    And the selected ingredient ID is 42
    And no initial search request fires until the user modifies the input

  Scenario: Units load in parallel with dialog initialization
    Given the user opens the "Add Item" inventory dialog
    When the dialog initializes
    Then the units of measure request fires immediately, in parallel with initial rendering
    And the units request is not chained after any ingredient request
    And units are available for selection as soon as their response arrives

  Scenario: Ingredient selection validation still works
    Given the user has typed into the ingredient autocomplete
    When the user submits the form without selecting an ingredient from the list
    Then the form shows a validation error requiring an ingredient selection
    And the form cannot be submitted until a valid ingredient is selected

  Scenario: Recipe create rows use the shared autocomplete instead of a full-list select
    Given the user is on the recipe create page
    And each ingredient row previously rendered a mat-select over all 120,505 canonical ingredients
    When the page loads
    Then each ingredient row renders the shared IngredientAutocompleteComponent instead
    And the allowCreate input is disabled on the recipe create page
    And no mat-select over the full ingredient list exists anywhere on the page

  Scenario: Selecting an ingredient in a recipe row sets that row's canonical ingredient ID
    Given the user is editing an ingredient row on the recipe create page
    When the user types "basil" into the shared autocomplete and selects "Fresh Basil" from the results
    Then the row's canonicalIngredientId is set to the ID of "Fresh Basil"
    And the autocomplete displays "Fresh Basil" as the selected value

  Scenario: Shared component is the single owner of search behaviour
    Given the IngredientAutocompleteComponent is a standalone Angular component implementing ControlValueAccessor
    When the inventory dialog and the recipe create page both use it
    Then debounce timing (250 ms), minimum character threshold (2), and stale-request cancellation are implemented only in the shared component
    And neither consumer duplicates any of that logic

  Scenario: No remaining caller requests the full ingredient list
    Given the shared IngredientAutocompleteComponent replaces all previous ingredient selection controls
    When every consumer of GET /api/v1/referencedata/ingredients is accounted for
    Then no frontend component calls the endpoint without a non-blank search parameter
    And the old ReferenceDataService.getIngredients() method that fetched the full list is removed or unreachable

  Scenario: Prefix match beats higher reference count
    Given canonical ingredients "Apple" (38,510 recipe references) and "Pineapple" (67,693 recipe references) exist
    When I search with search="apple"
    Then "Apple" appears before "Pineapple" in the results
    Because "Apple" is a prefix match and "Pineapple" is a substring match

  Scenario: Higher reference count ranks first within the prefix-match group
    Given canonical ingredients "Chicken Breast" (85,000 recipe references) and "Chicken Feet" (1,200 recipe references) both start with "chicken"
    When I search with search="chicken"
    Then "Chicken Breast" appears before "Chicken Feet" in the results
    Because both are prefix matches and "Chicken Breast" has a higher RecipeReferenceCount

  Scenario: Name breaks ties when reference counts are equal
    Given canonical ingredients "Basil" and "Bay Leaf" both have 5,000 recipe references and both start with "ba"
    When I search with search="ba"
    Then "Basil" appears before "Bay Leaf" in the results
    Because both are prefix matches with equal RecipeReferenceCount and "Basil" sorts before "Bay Leaf" alphabetically

  Scenario: Migration backfills RecipeReferenceCount from existing RecipeIngredients
    Given the CanonicalIngredients table has 120,505 rows with RecipeReferenceCount defaulting to 0
    And the RecipeIngredients table contains 13,635,157 rows
    When the EF Core migration runs via "dotnet ef database update"
    Then every CanonicalIngredient's RecipeReferenceCount is set to the number of RecipeIngredient rows referencing it
    And canonical ingredients with no recipe references retain a count of 0

  Scenario: API recipe import increments RecipeReferenceCount
    Given a canonical ingredient "Saffron" has a RecipeReferenceCount of 412
    When a new recipe is imported through POST /api/v1/recipes with 1 RecipeIngredient referencing "Saffron"
    Then "Saffron" RecipeReferenceCount increases to 413

  Scenario: Kaggle ingest recomputes RecipeReferenceCount at end of run
    Given the MealsEnPlace.Tools.Ingest tool has finished inserting all recipes from a Kaggle dataset
    When the ingest run completes
    Then RecipeReferenceCount is recomputed for every CanonicalIngredient from the full RecipeIngredients table
    And the counts reflect the complete post-ingest state, not incremental updates

  Scenario: Search completes well under one second on the full table
    Given the CanonicalIngredients table contains 120,505 rows with a populated RecipeReferenceCount column
    And the RecipeIngredients table contains 13,635,157 rows
    When I search with search="ch" (a broad term matching thousands of candidates)
    Then the response returns in under 500 ms
    And the query uses the stored RecipeReferenceCount rather than a live correlated COUNT
```

---

## [MEP-049] NER Token Normalization at Ingest Time

**Status:** Done
**Priority:** Medium
**Depends on:** MEP-026 (the Kaggle ingest pipeline this story modifies), MEP-048 (the search that exposed the junk rows)

### Implementation Notes
Shipped as a pure `NerTokenNormalizer` in `MealsEnPlace.Tools.Ingest`. Rule order: trim;
strip leading/trailing characters that are not letters or digits (so wrapping quotes,
brackets, slashes, and periods are removed while internal apostrophes and hyphens survive);
collapse internal whitespace; repeatedly strip a trailing stopword but never the last
remaining word; reject empty, reject no-letters, reject all-stopwords. Stopword set:
{a, add, an, and, for, of, or, plus, the, to, with}.

`Program.cs` normalizes each row's NER list once up front and feeds the cleaned list to both
the pre-create loop and the best-match picker, so a rejected token never creates a
CanonicalIngredient row and fallback to the next-best match is automatic.
`GetOrCreate` also normalizes defensively and routes rejections to the existing "unknown"
row. `IngestSummary` reports NER tokens normalized and rejected.

Two review findings fixed before close: (1) a token truncated at 200 characters could keep
a trailing space -- now `TrimEnd` runs after the cut; (2) apostrophes were originally allowed
at the edges, so `'apple'` kept its wrapping quotes -- edge stripping now removes them.

Verification: 115 ingest-scoped unit tests pass; `NerTokenNormalizer` and
`CanonicalIngredientRegistry` at 100% line coverage. A 20,000-row dry run against the
user's CSV completed and reported 39 normalized / 26 rejected tokens.

Full re-ingest verification (2026-09-10, 42 minutes): 2,231,142 rows read, 588,044
Recipes1M rows skipped, 1,643,098 recipes ingested, 143,107 canonical ingredients created,
14,833 NER tokens normalized, 31,131 rejected, reference counts backfilled, zero stderr
output. Post-ingest checks: 0 canonical names with a non-alphanumeric leading or trailing
character; the "apple" search returns Apples, apple, apple cider vinegar, applesauce, apple
juice, apple cider first with none of the punctuation variants present.

Scope decision during verification: the reset procedure was missing the MEP-038 Dedup tool
(`src/MealsEnPlace.Tools.Dedup`) as a final step. The dedup tool is a separate offline pass
that folds plural and prep-modifier variants (e.g., "Apples" into "apple"); it is not part
of the ingest, so a fresh ingest lands at ~143k canonical rows until the dedup runs. A dry
run on the new database projected 15,261 fold groups, 25,289 loser rows, and 2,931,374
RecipeIngredient reassignments. The dedup tool has also been updated on this branch to
recompute `RecipeReferenceCount` at the end of a live run, mirroring the ingest tool. The
README now documents the full procedure as: reset database, apply migrations, run ingest,
run dedup dry-run to review, run dedup live. Both tools recompute reference counts.

Follow-on candidate (not a new item): 3,640 names still contain internal punctuation such
as "parmesan/romano", "chili_powder", "preserves(blueberry", "oreo® cookies", and
stopword-free fragments like "a crowd" and "type fruit" survive because only trailing
connectives are stripped.

### Business Problem
The Kaggle bulk ingest (MEP-026) feeds every NER-column token through
`CanonicalIngredientRegistry.GetOrCreate`, which trims whitespace and truncates at 200
characters but applies no further normalization. The Kaggle NER column is noisy: tokens
arrive with leading or trailing punctuation, dangling brackets, embedded slashes, trailing
connectives ("apple and", "apple add"), and fragments that contain no letters at all ("a
crowd", "and", "type fruit"). Each noisy token becomes its own CanonicalIngredient row.

After MEP-038's morphological deduplication pass the table holds 120,505 rows. Of those,
1,480 names have leading or trailing punctuation or brackets; 6,381 names contain a character
other than letters, spaces, apostrophes, or hyphens; and 2,434 rows are referenced by zero
RecipeIngredients (1,902 of those are in the punctuation set). The MEP-048 ingredient search
makes the problem user-visible: searching "apple" shows "apple", "apple [", "apple.",
"apple/", and "apple add" side by side. "apple" is referenced by 38,510 RecipeIngredients;
the four junk variants by a combined total of 2.

The fix belongs in the importer, not in a repair migration over existing rows. Normalizing
tokens at ingest time prevents junk from entering the table in the first place. After the
normalization rules are in place the user will run a full re-ingest from a clean database.
The re-ingest procedure is a full reset: drop and recreate the Postgres database (or
recreate the Docker volume), apply all EF Core migrations so seed data lands, then run the
ingest tool once. All existing data -- inventory items, user-created ingredients, recipes --
is wiped and rebuilt from scratch.

The ingest tool currently has no documented reset or re-ingest procedure (the README
documents `--csv`, `--dry-run`, and `--max-rows` only). This story must also document the
full reset-and-ingest procedure so the user can repeat it confidently. The documentation
must warn that the ingest tool does not detect previously imported recipes: running it twice
against the same database duplicates every recipe.

Semantic merging of true synonyms (e.g., "bell pepper" vs "sweet pepper") remains in
MEP-038's domain and is out of scope here.

### Acceptance Criteria
```gherkin
Feature: NER Token Normalization at Ingest Time

  Scenario: Leading and trailing punctuation and brackets are stripped
    Given a Kaggle NER token "apple ["
    When the normalization step runs
    Then the normalized value is "apple"
    And the CanonicalIngredient row is stored with name "apple"

  Scenario: Trailing punctuation variants collapse to the base name
    Given Kaggle NER tokens "apple.", "apple/", and "apple"
    When each token is normalized and passed to GetOrCreate
    Then all three resolve to the same CanonicalIngredient row with name "apple"

  Scenario: Internal whitespace is collapsed
    Given a Kaggle NER token "  red   bell   pepper  "
    When the normalization step runs
    Then the normalized value is "red bell pepper"

  Scenario: Trailing connective is stripped rather than rejecting the token
    Given Kaggle NER tokens "apple and" and "apple add"
    When the normalization step runs
    Then both normalize to "apple"
    And both resolve to the same CanonicalIngredient row as a plain "apple" token

  Scenario: Token normalizing to empty is rejected
    Given a Kaggle NER token consisting only of punctuation (e.g., "[", "//")
    When the normalization step runs
    Then the token is rejected
    And no CanonicalIngredient row is created for it
    And the raw ingredient falls back to the next-best NER match or existing unknown handling

  Scenario: Token containing no letters is rejected
    Given a Kaggle NER token "1/2" or "3.5"
    When the normalization step runs
    Then the token is rejected because it contains no alphabetic characters
    And no CanonicalIngredient row is created for it

  Scenario: Stopword-only token is rejected
    Given a Kaggle NER token "and" or "a" or "of the" or "for" or "with"
    When the normalization step runs
    Then the token is rejected because it consists entirely of English stopwords or connectives
    And no CanonicalIngredient row is created for it
    And the stopword list includes at minimum: a, an, the, and, or, of, for, with, to, add, plus

  Scenario: Normalization rules are pure functions with unit tests
    Given the normalization logic is implemented as pure functions
    When the unit test suite in tests/MealsEnPlace.Unit/Tools/Ingest runs
    Then the five apple examples ("apple", "apple [", "apple.", "apple/", "apple add") all normalize to "apple"
    And edge cases for empty, no-letter, and stopword-only tokens are covered
    And the tests are independent of database state

  Scenario: Ingest summary reports normalization and rejection counts
    Given a full Kaggle ingest completes
    When the summary is printed
    Then it reports the count of tokens that were normalized (original differed from stored value)
    And the count of tokens that were rejected (did not produce a CanonicalIngredient row)

  Scenario: Full re-ingest produces no punctuation-fragment ingredient names
    Given the normalization rules are deployed in CanonicalIngredientRegistry
    And the user runs a full re-ingest against the Kaggle CSV
    When the ingest completes
    Then no CanonicalIngredient name matches the pattern of leading or trailing punctuation or brackets
    And searching "apple" returns "apple" without "apple [", "apple.", "apple/", or "apple add" variants

  Scenario: Full database reset procedure is documented
    Given the ingest tool's README (src/MealsEnPlace.Tools.Ingest/README.md) currently has no reset or re-ingest documentation
    When MEP-049 ships
    Then the README documents the step-by-step procedure: drop and recreate the Postgres database (or recreate the Docker volume), apply all EF Core migrations with "dotnet ef database update --project src/MealsEnPlace.Api" so seed data lands, run the ingest tool once, run the MEP-038 Dedup tool with --dry-run to review projected folds, then run the Dedup tool live
    And the procedure states explicitly that all existing data (inventory, recipes, user-created ingredients) is wiped
    And both the ingest and dedup tools recompute RecipeReferenceCount at the end of a live run

  Scenario: README warns against running the ingest tool twice without resetting
    Given the ingest tool does not detect previously imported recipes
    When a user runs the ingest tool against a database that already contains ingested recipes
    Then every recipe in the CSV is inserted again, duplicating the entire catalog
    And the README states that the database must be reset before re-ingesting
    And the README labels this as a destructive operation that cannot be undone
```

---

## [MEP-050] Canonical Ingredient Normalization Gaps: Preservation State, Typos, Brands, Filler, and URL Rejection

**Status:** Done
**Priority:** Medium
**Depends on:** MEP-038 (dedup tooling this story extends), MEP-049 (NER normalization and re-ingest procedure this story reuses)

### Business Problem
A data-quality investigation into the `CanonicalIngredients` table -- prompted by searching "pea" and finding 398 near-duplicate rows -- uncovered several categories of ingredient-name duplication that the existing MEP-038 fold-group approach and MEP-049 NER token normalization do not catch. `CanonicalNameNormalizer` (in `src/MealsEnPlace.Tools.Dedup/CanonicalNameNormalizer.cs`) builds a fold-group key by lowercasing, splitting on space/tab/comma/parens/hyphen, dropping a flat stopword list of cosmetic prep words, singularizing, and sorting tokens. `FoldGroupResolver` folds any two names that produce the same key. This works for pure prep/plural noise but misses five distinct failure modes, all found among the "pea" duplicates but generalizable to the whole ~120k-row canonical ingredient table:

1. **Preservation-state words are miscategorized as cosmetic.** The current stopword list includes `fresh`, `frozen`, `dried`, `cooked`, `raw`, and `uncooked` alongside pure prep-cut words like `chopped` and `diced`. This folded `frozen peas`, `fresh peas`, `cooked peas`, and `dried peas` all into a single `pea` canonical (confirmed via `CanonicalIngredientAliases`) -- which is wrong. Preservation state changes how an ingredient is stored, purchased, and used in a recipe. `fresh peas` and `frozen peas` must be distinct `CanonicalIngredient` rows, the same way `baby carrot` is already distinct from `carrot`. The six preservation-state words (`fresh`, `frozen`, `dried`, `cooked`, `raw`, `uncooked`) must be removed from the stopword list and treated as substantive. Pure prep-cut words (`chopped`, `crushed`, `cubed`, `cut`, `diced`, `grated`, `ground`, `halved`, `minced`, `peeled`, `quartered`, `seeded`, `shredded`, `sliced`, `trimmed`, `whole`) stay cosmetic and keep folding as today.

   **Critical constraint:** the MEP-038 dedup pass is destructive -- it deletes loser `CanonicalIngredient` rows and only records the folded name string in `CanonicalIngredientAliases`, with no record of which specific `RecipeIngredient` row originated from which pre-fold name. `frozen peas` cannot be surgically split back out of the current `pea` row because there is no way to know which of `pea`'s 17,631 `RecipeIngredient` references were originally "frozen peas" vs "fresh peas" vs plain "peas." The only correct fix is the full reset-and-re-ingest procedure documented in MEP-049: drop/recreate the Postgres database, re-run the ingest tool against the user's Kaggle CSV, re-run the Dedup tool. This wipes inventory items, user-created ingredients, and meal plans -- same consequence as MEP-049.

2. **Typos are not caught at all.** Examples: `frozed peas`, `spit peas`, `slit peas`, `sping peas`, `yellow splitt peas`, `earlie peas`, `pidgeaon peas`, and three misspellings of the Le Sueur brand (`lesuer`, `leseur`, `lesueuer`). The fix is a small hand-curated typo/synonym dictionary applied as an extra normalization step before the token-set key is built. Automatic fuzzy/edit-distance matching is explicitly rejected because it is dangerous in this domain -- `pea` and `pear` are one edit apart, and automatic distance-based folding could silently corrupt recipe matching data. The dictionary must also cover compound-word vs. split-word synonyms that the token-set approach cannot catch because they do not share tokens at all: `chickpea` / `chick pea`, and `black-eyed` / `black eyed` / `blackeyed` / `black eye` (all currently separate canonicals; `back eyed peas` and `blacck eyed peas` are typos of the same group).

3. **Brand names are not stripped.** Examples: `lesueur peas`, `lesueur green peas`, `del monte peas`, `del monte sugar peas`, `campbell's pea soup`, `birds eye sweet peas`, `green giant baby early peas`, `green giant frozen sweet peas`, `knorr green peas`. A new brand-name stopword category (separate from the prep-cut list) should be added to `CanonicalNameNormalizer` so brand words strip out the same way prep words do (e.g., `lesueur peas` folds to `pea`, `campbell's pea soup` folds to `pea soup`). This list will grow over time as more brands surface across the wider catalog, not just peas -- it should live somewhere clearly extensible (e.g., a separate file or configuration section rather than inline in the normalizer method).

4. **Leading filler/quantity words and recipe-authoring artifacts are not caught.** Examples: `handful of peas`, `handful snow peas`, `bags of frozen peas`, `bags peas`, `packets frozen peas`, `mugful frozen peas`, `kilogram snow peas`, `gallon peas`, `pints peas`, and non-ingredient phrasing artifacts `peas optional`, `peas and/or`, `choice of peas`, `either peas`, `peas etc`, `peas - if`, `e.g. peas`. A second new stopword category (filler/quantity/authoring-artifact words) should strip these the same way. Additionally, `CanonicalNameNormalizer.Normalize` splits on `[' ', '\t', ',', '(', ')', '-']` but not `/`, so tokens like `peas/carrots`, `chickpeas/garbanzo beans`, and `peanut/vegetable oil` never tokenize correctly. `/` must be added to the split-character set.

5. **URLs leak into canonical ingredient names.** Two `CanonicalIngredient` rows are entire Food Network URLs (e.g., `http://www.foodnetwork.com/recipes/paula-deen/sure-fire-no-fire-smores-recipe/index.html?oc=linkback`) that were extracted from the Kaggle NER column as ingredient names. `NerTokenNormalizer` (in `src/MealsEnPlace.Tools.Ingest/NerTokenNormalizer.cs`) strips edge punctuation but never rejects a token containing a URL shape, so these pass through as valid canonical ingredients. A rejection rule must be added to `NerTokenNormalizer.Normalize` for any token containing `://` (or otherwise matching a URL shape), following the same rejection pattern already used for `EmptyAfterCleanup`, `NoLetters`, and `StopwordsOnly`. This is a different bug from MEP-037, which handles ad/tracking URLs in the Kaggle row's `link` field (`Recipe.SourceUrl`); this bug is about URLs leaking into the NER ingredient token column and becoming `CanonicalIngredient` rows -- an unrelated column and unrelated failure mode. Not scoped to peas; likely present across the whole catalog.

**Non-goal:** `snow pea`, `sugar snap pea`, `black-eyed peas`, `split peas` (yellow and green stay separate), `chickpea`, and `pigeon pea` are genuine distinct ingredients and sub-varieties. They must NOT be folded together. Nothing in this story should fold them, and acceptance criteria verify that they survive intact.

**Recommended sequencing:** Items 2, 3, and 4 (typo/synonym dictionary, brand stopwords, filler stopwords, `/` delimiter) are non-destructive against the current database -- those duplicate rows still exist un-folded today, so extending `CanonicalNameNormalizer` / `FoldGroupResolver` and re-running `MealsEnPlace.Tools.Dedup --dry-run` then live folds them without requiring a reset. Item 5 (URL rejection in `NerTokenNormalizer`) only affects future ingests, not current data, unless bundled with a reset. Item 1 (preservation-state un-fold) strictly requires the full reset-and-re-ingest procedure because it is undoing an already-applied destructive fold. Recommendation: land all normalizer/ingest changes (items 1 through 5) together, then do exactly one reset, re-ingest, `Dedup --dry-run`, `Dedup` (live) cycle rather than doing a non-destructive dedup pass now and a second reset later.

### Verification note

Verified against a full reset-and-re-ingest of the live database (2026-09-13): 143,049 raw
canonical rows folded to 117,500. `fresh pea` (420 refs), `frozen pea` (9,112 refs), and
`pea` (8,445 refs) are now three distinct canonicals, confirming the preservation-state
split. `lesueur peas`, `del monte peas`, `campbell's pea soup`, `birds eye sweet peas`, and
`knorr green peas` all folded into their non-branded survivor (recorded as aliases);
`chickpea`/`chick pea`/`chickpeas` collapsed to one row; the two Food Network URL rows are
gone and no `://`-shaped name remains anywhere in the catalog.

Two known residual gaps, neither blocking: (1) a brand-name phrase only strips from a name
when a non-branded duplicate exists to fold into -- a singleton with no such duplicate (e.g.
`green giant baby early peas`) keeps its brand in the display name even though its fold key
is brand-free, since this dedup pass merges duplicates rather than renaming unique rows; (2)
`black-eye peas` (hyphenated, no trailing "d") wasn't added to the typo dictionary's
`black eye` (space-separated) pattern, so it didn't join the `black eyed` family. Also
unrelated to this story: one pre-existing degenerate canonical named literally `http` (2
refs) predates the URL fix and isn't a full URL, so the `://` rejection rule doesn't apply to
it.

Also discovered during the live run: Npgsql's default 30-second command timeout is too short
for a bulk `UPDATE` against the 14M-row `RecipeIngredients` table when a fold group's loser
has a very large reference count. The first live attempt aborted partway through (partial
progress preserved safely -- see `CanonicalIngredientDedupRunner`'s per-batch transactions);
a retry with `Command Timeout=300` on the connection string completed cleanly. Documented in
`src/MealsEnPlace.Tools.Dedup/README.md`.

### Acceptance Criteria
```gherkin
Feature: Canonical Ingredient Normalization Gaps

  Scenario: Preservation-state words are treated as substantive, not cosmetic
    Given CanonicalNameNormalizer's stopword list currently includes "fresh", "frozen", "dried", "cooked", "raw", and "uncooked"
    When the stopword list is corrected
    Then "fresh", "frozen", "dried", "cooked", "raw", and "uncooked" are removed from the cosmetic stopword list
    And "fresh peas" and "frozen peas" produce different fold-group keys
    And "chopped peas" and "diced peas" still produce the same fold-group key as "peas"

  Scenario: Preservation-state correction requires full reset-and-re-ingest
    Given the MEP-038 dedup pass destructively deleted loser CanonicalIngredient rows
    And CanonicalIngredientAliases records only the folded name string, not which RecipeIngredient rows originated from which pre-fold name
    When the preservation-state stopword correction is deployed
    Then the full reset-and-re-ingest procedure (MEP-049) is executed: drop/recreate the Postgres database, apply migrations, run ingest, run Dedup --dry-run, run Dedup live
    And the procedure wipes inventory items, user-created ingredients, and meal plans
    And the re-ingest documentation is updated to note this consequence

  Scenario: Typo/synonym dictionary corrects known misspellings before fold-key computation
    Given a hand-curated typo/synonym dictionary is configured
    And the dictionary maps "frozed" to "frozen", "spit" to "split", "slit" to "split", "sping" to "snap", "splitt" to "split", "earlie" to "early", "pidgeaon" to "pigeon"
    When CanonicalNameNormalizer processes the token "frozed peas"
    Then the token normalizes as if it were "frozen peas"
    And "spit peas" normalizes as "split peas"
    And "pidgeaon peas" normalizes as "pigeon peas"

  Scenario: Typo/synonym dictionary corrects Le Sueur brand misspellings
    Given the dictionary maps "lesuer", "leseur", and "lesueuer" to "lesueur"
    When CanonicalNameNormalizer processes "lesuer peas"
    Then the token normalizes the same as "lesueur peas"
    And after brand stripping (see brand-name scenario), all resolve to "pea"

  Scenario: Typo/synonym dictionary handles compound-word and split-word synonyms
    Given the dictionary maps "chickpea" to "chick pea" (or vice versa) as a compound synonym
    And the dictionary maps "blackeyed" to "black-eyed", "black eye" to "black-eyed"
    When CanonicalNameNormalizer processes "chickpea", "chick pea", "black-eyed peas", "blackeyed peas", "black eye peas"
    Then "chickpea" and "chick pea" produce the same fold-group key
    And "black-eyed peas", "blackeyed peas", and "black eye peas" produce the same fold-group key
    And "back eyed peas" (typo) and "blacck eyed peas" (typo) also resolve to the same key via the typo dictionary

  Scenario: No fuzzy or edit-distance matching is used
    Given the typo correction uses only a hand-curated dictionary
    When "pea" and "pear" are processed
    Then they remain distinct fold-group keys despite being one edit apart
    And no automatic distance-based folding is applied

  Scenario: Brand names are stripped via a brand-name stopword category
    Given a brand-name stopword list is configured separately from the prep-cut stopword list
    And the list includes "lesueur", "del monte", "campbell's", "birds eye", "green giant", "knorr"
    When CanonicalNameNormalizer processes "lesueur peas"
    Then the fold-group key matches that of "peas"
    And "del monte sugar peas" folds to the same key as "sugar peas"
    And "campbell's pea soup" folds to the same key as "pea soup"
    And "green giant frozen sweet peas" folds to the same key as "frozen sweet peas"

  Scenario: Brand-name stopword list is extensible
    Given the brand-name list will grow as more brands surface across the wider catalog
    When the list is implemented
    Then it lives in a clearly extensible location (separate file, configuration section, or dedicated constant collection) rather than inline in the normalizer method

  Scenario: Filler, quantity, and authoring-artifact words are stripped
    Given a filler/quantity/authoring-artifact stopword list includes "handful", "bags", "packets", "mugful", "kilogram", "gallon", "pints", "optional", "etc", "choice", "either", "e.g"
    When CanonicalNameNormalizer processes "handful of peas"
    Then the fold-group key matches that of "peas"
    And "bags of frozen peas" folds to the same key as "frozen peas"
    And "peas optional" folds to the same key as "peas"
    And "e.g. peas" folds to the same key as "peas"

  Scenario: Forward slash is added to the split-character set
    Given CanonicalNameNormalizer.Normalize currently splits on space, tab, comma, parens, and hyphen
    When "/" is added to the split-character set
    Then "peas/carrots" tokenizes into "peas" and "carrots"
    And "chickpeas/garbanzo beans" tokenizes into "chickpeas", "garbanzo", and "beans"
    And "peanut/vegetable oil" tokenizes into "peanut", "vegetable", and "oil"

  Scenario: URL-shaped NER tokens are rejected at ingest time
    Given a Kaggle NER token is "http://www.foodnetwork.com/recipes/paula-deen/sure-fire-no-fire-smores-recipe/index.html?oc=linkback"
    When NerTokenNormalizer.Normalize processes the token
    Then the token is rejected with a reason analogous to "EmptyAfterCleanup", "NoLetters", or "StopwordsOnly"
    And no CanonicalIngredient row is created for it
    And IngestSummary reports the rejection

  Scenario: URL rejection is distinct from MEP-037 ad/tracking URL stripping
    Given MEP-037 handles ad/tracking URLs in the Kaggle row's link field (Recipe.SourceUrl)
    When a URL leaks into the NER ingredient token column
    Then the NerTokenNormalizer URL rejection catches it
    And the fix applies to any URL shape (containing "://"), not only ad/tracking patterns

  Scenario: Genuine distinct ingredients are NOT folded together
    Given CanonicalIngredient rows exist for "snow pea", "sugar snap pea", "black-eyed peas", "split peas", "chickpea", and "pigeon pea"
    When the full normalization and dedup pipeline runs
    Then each remains a distinct CanonicalIngredient row
    And "snow pea" is not folded into "pea"
    And "sugar snap pea" is not folded into "pea"
    And "black-eyed peas" is not folded into "pea"
    And "split peas" is not folded into "pea"
    And "chickpea" is not folded into "pea"
    And "pigeon pea" is not folded into "pea"
    And yellow split peas and green split peas remain separate

  Scenario: All changes land together with a single reset-and-re-ingest cycle
    Given items 2, 3, and 4 (typo dictionary, brand stopwords, filler stopwords, "/" delimiter) are non-destructive
    And item 5 (URL rejection) only affects future ingests
    And item 1 (preservation-state un-fold) requires a full reset
    When all five changes are implemented
    Then exactly one reset, re-ingest, Dedup --dry-run, Dedup (live) cycle is performed
    And no intermediate non-destructive dedup pass followed by a second reset is needed

  Scenario: Dry-run reports projected impact before live dedup
    Given all normalizer changes have been deployed
    And a fresh re-ingest has completed
    When the user runs MealsEnPlace.Tools.Dedup --dry-run
    Then the tool reports the projected fold groups, alias inserts, and per-table FK reassignment counts
    And the user can review the output before running the live pass
```

---

## [MEP-051] Upgrade to Vitest 5 / @vitest/coverage-v8 5

**Status:** Blocked
**Priority:** Low
**Depends on:** stable `@angular/build` release accepting `vitest ^5.0.0`

### Business Problem
Dependabot proposed bumping `@vitest/coverage-v8` from 4.1.11 to 5.0.0
(PR #146). Investigation revealed this upgrade is currently unsafe.
`@vitest/coverage-v8@5.0.0` has a peer dependency requiring `vitest@5.0.0`,
but every stable release of `@angular/build` (through 22.1.8) pins its
`vitest` peer dependency to `^4.0.8`. The project's Angular test runner goes
through the `@angular/build:unit-test` builder (configured in `angular.json`),
so vitest cannot be upgraded independently of `@angular/build`.

Only a `next`-tagged prerelease (`@angular/build@22.2.0-next.7`) accepts
`vitest ^5.0.0` -- there is no stable Angular tooling release that supports
it. Bumping the coverage package alone creates an immediate peer-dependency
conflict. Bumping both together forces pulling in prerelease Angular build
tooling as a devDependency, which is not acceptable for this project.

PR #146 was closed with an explanatory comment. This backlog item tracks the
blocked upgrade so it can be picked up once the dependency constraint clears.

**Unblocked when:** A stable (non-prerelease) version of `@angular/build` is
published that declares `vitest ^5.0.0` (or broader) in its peer dependencies.
At that point, `@angular/build`, `vitest`, and `@vitest/coverage-v8` can all
be bumped together in a single coordinated upgrade.

### Acceptance Criteria
```gherkin
Feature: Vitest 5 Upgrade

  Scenario: Upgrade vitest and coverage package together
    Given a stable release of @angular/build accepts vitest ^5.0.0 in its peer dependencies
    When the developer upgrades @angular/build, vitest, and @vitest/coverage-v8 together
    Then npm install completes with no peer-dependency warnings or errors
    And the @angular/build:unit-test builder runs all existing test suites successfully
    And coverage collection via @vitest/coverage-v8 reports results without errors
    And the 90% coverage threshold configured in angular.json is still enforced

  Scenario: No partial upgrade
    Given vitest 5 is available but @angular/build stable does not yet accept it
    When a developer or Dependabot proposes bumping only @vitest/coverage-v8 to 5.x
    Then the proposal is declined
    And this backlog item remains in Blocked status

  Scenario: Prerelease tooling is not used
    Given only a prerelease (next-tagged) version of @angular/build accepts vitest ^5.0.0
    When evaluating the upgrade
    Then the upgrade is deferred until a stable release is available
    And no prerelease @angular/build version is added to devDependencies
```

---

## [MEP-052] Claude Model Selector in Settings

**Status:** Done
**Priority:** Medium

### Implementation Notes
Shipped on branch `feature/mep-052-claude-model-selector`. Scope covered:

- `ClaudeModel` enum (`Fable51`, `Haiku45`, `Opus5`, `Sonnet5`) and `ClaudeModelCatalog`
  mapping each member to its Anthropic API model ID and display name, plus a
  `TryParse` that falls back to `Default` (`Sonnet5`) for null, empty, or unrecognized
  input — covers the "invalid/deprecated model ID" scenario without erroring the page.
- `IClaudeModelStore` / `ClaudeModelStore`: a plain-text file store (`claude-model.txt`
  under `%LOCALAPPDATA%/MealsEnPlace/`), separate from the DataProtection-encrypted
  token store since the model choice is not a secret.
- `SettingsController`: `POST /api/v1/settings/claude/model` persists the selection
  (400 for an unrecognized name); `GET /claude/status`, `POST /claude/token`, and
  `DELETE /claude/token` all now return the current model alongside `configured` —
  clearing the key leaves the model preference untouched.
- `AnthropicTestClient` (the one real outbound Claude call in the codebase, per
  MEP-032's scope decision) now reads the model preference on every `PingAsync`
  call instead of a hardcoded constant, so Test Connection verifies the user's
  actual model choice and a Settings-page change takes effect without a restart.
- Angular: `ClaudeModel` type and extended `ClaudeTokenStatusResponse` in
  `settings.models.ts`; `SettingsService.saveModel`; `AiAvailabilityService` now
  tracks `model` alongside `configured`; `SettingsPageComponent` adds a `mat-select`
  model picker (Opus 5 / Sonnet 5 / Haiku 4.5 / Fable 5.1, best-to-cheapest order)
  to the existing AI card, visible and usable with no API key configured.

### Scope decisions
- **The stubbed `IClaudeService` methods are untouched.** Per MEP-032, dietary
  classification, UOM resolution, matching feasibility/substitution, and meal plan
  optimization do not yet issue real Anthropic calls — there is nothing to wire the
  model preference into on those paths until they are converted to real Claude calls
  in a future story. `IClaudeModelStore` is the seam they will read from at that point.
- **No per-feature-type model assignment.** One global preference applies uniformly,
  as scoped. Per-call-site model selection remains a possible future enhancement.

### Business Problem
The app hardcodes a single Claude model for every AI-backed call. Because the user brings their own Anthropic API key and pays per token (MEP-032), model choice is a meaningful cost/quality/speed tradeoff. Lightweight operations such as colloquial unit-of-measure resolution and container reference flagging could run against a cheaper, faster model (e.g., Haiku), while higher-stakes calls -- dietary classification, recipe matching feasibility and substitution, meal plan optimization, and the future MEP-012 flyer Vision extraction -- benefit from a stronger model (e.g., Sonnet or Opus). Today the user has no way to change the model without a code change and redeploy.

This story adds a model picker to the existing AI section of the Settings page so the user can choose the Claude model that all AI-backed calls use. The selection persists server-side alongside the encrypted API key (it is not a secret and may be returned plainly in the settings status response). A future enhancement could allow per-feature-type model assignment (e.g., Haiku for UOM resolution, Opus for meal plan optimization), but that is out of scope here -- a single global model preference covers the MVP need.

### Acceptance Criteria
```gherkin
Feature: Claude Model Selector in Settings

  Scenario: Model picker appears in the AI section of the Settings page
    Given the user navigates to the Settings page
    When the AI section renders
    Then a model dropdown is visible below the API key controls
    And the dropdown lists the current Claude model family: Opus 5, Sonnet 5, Haiku 4.5, Fable 5.1
    And the default selection is Sonnet 5 if the user has not previously chosen a model

  Scenario: User selects a different model
    Given the model dropdown is displaying the current selection
    When the user selects "Haiku 4.5" from the dropdown
    And clicks Save (or the selection auto-saves)
    Then the backend persists the model preference server-side
    And the Settings page confirms the selection was saved

  Scenario: Selected model persists across app restarts
    Given the user has selected "Opus 5" as the preferred model
    When the app is restarted and the user returns to the Settings page
    Then the model dropdown shows "Opus 5" as the current selection

  Scenario: Model preference applies to all Claude-backed calls
    Given the user has selected "Haiku 4.5" as the preferred model
    When any Claude-backed operation runs (UOM resolution fallback, dietary classification, recipe matching feasibility/substitution, meal plan optimization)
    Then the Anthropic API request uses the model ID corresponding to "Haiku 4.5"
    And no call uses a different model

  Scenario: Model change takes effect without app restart
    Given the user changes the model from "Sonnet 5" to "Opus 5"
    When the next Claude-backed call is triggered
    Then that call uses the model ID corresponding to "Opus 5"
    And no app restart or redeployment is required

  Scenario: Model preference is not a secret
    Given the user has selected a model
    When the frontend calls GET /api/v1/settings/claude/status
    Then the response includes the selected model ID in plaintext alongside the existing { configured: bool } indicator
    And the raw API key is still never included

  Scenario: Model picker renders when no API key is configured
    Given no Claude API key has been saved
    When the user views the AI section of the Settings page
    Then the model dropdown is visible and interactive
    And the user can select and save a model preference
    And no Claude API calls are triggered by saving the preference
    And the selection is ready to take effect once a key is configured

  Scenario: Invalid or deprecated model ID falls back to the default
    Given the persisted model preference contains a value that is no longer recognized (e.g., a model removed in a newer app version)
    When the Settings page loads
    Then the dropdown shows the default model (Sonnet 5) instead of the unrecognized value
    And when the next Claude-backed call runs it uses the default model
    And no error is shown on the Settings page
```
