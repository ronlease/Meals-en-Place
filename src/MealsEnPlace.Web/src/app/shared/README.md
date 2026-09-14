# Shared

Reusable UI components used across feature modules.

## Components

| Component | Description |
|---|---|
| `IngredientAutocompleteComponent` | Debounced server-side ingredient search autocomplete implementing `ControlValueAccessor`. Emits the selected `CanonicalIngredientDto` (or null) as its form value. Accepts an optional `allowCreate` input that shows a "Create &lt;name&gt;" option when no exact match exists. Owns the 250 ms debounce, 2-character minimum, and `rxResource`-based request cancellation. |
| `OfflineBannerComponent` | Displays an amber banner when the browser is offline, informing the user that cached data is being shown. Driven by `NetworkStatusService`. |
