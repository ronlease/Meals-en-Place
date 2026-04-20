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

**Status:** Done
**Priority:** High

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

**Status:** Backlog
**Priority:** Low

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

**Status:** Backlog
**Priority:** Low

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

**Status:** Backlog
**Priority:** Medium

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

**Status:** Backlog
**Priority:** Medium

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

**Status:** Backlog
**Priority:** Medium

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

**Status:** Backlog
**Priority:** Low
**Depends on:** MEP-026 (Kaggle ingest) must land first so the app has a working catalog source before TheMealDB is removed.

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

