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

**Status:** Backlog
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

**Status:** Backlog
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

**Status:** Backlog
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

## [MEP-012] Store Sale Integration

**Status:** Backlog
**Priority:** Low

### Business Problem
Knowing which ingredients are on sale at local grocery stores would help me save money by aligning my meal plan with current deals. This feature would integrate with store circulars to surface sale prices alongside my shopping list, enabling cost-conscious meal planning.

**Blocked Reason:** Most major grocery chains prohibit scraping of their circular and pricing data. Implementing this feature requires either a vendor API partnership or a paid aggregator service (e.g., Flipp API). Do not implement until a viable, legal data source is secured.

### Acceptance Criteria
```gherkin
Feature: Store Sale Integration

  Scenario: Display sale items alongside shopping list
    Given I have a generated shopping list
    And store sale data is available for my local area
    When I view my shopping list
    Then items currently on sale are highlighted with the sale price

  Scenario: Suggest meal plan adjustments based on sales
    Given store sale data is available
    When I generate a meal plan
    Then the system factors sale prices into recipe ranking
    And recipes using on-sale ingredients receive a cost bonus
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

**Status:** Backlog
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

**Status:** Backlog
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

**Status:** Backlog
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

**Status:** Backlog
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
