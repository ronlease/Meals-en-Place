# Core

Application-wide services and models, all `providedIn: 'root'`.

## Services

| Service | Description |
|---|---|
| `InstallPromptService` | Captures the `beforeinstallprompt` event for PWA install and exposes `canInstall` / `promptInstall()`. |
| `InventoryService` | CRUD for inventory items across Pantry, Fridge, and Freezer. |
| `MealPlanService` | Active meal plan retrieval, generation, and slot swapping. |
| `NetworkStatusService` | Signal-based wrapper around `navigator.onLine` and `online`/`offline` events. |
| `PreferencesService` | Display system preference (Imperial/Metric) with backend sync. |
| `PushNotificationService` | Stub service wiring `SwPush` for future push notifications. |
| `RecipeService` | Recipe library, import, matching, detail, and add-to-shopping-list. |
| `ReferenceDataService` | Canonical ingredients and units of measure for form dropdowns. |
| `SeasonalProduceService` | In-season and full-calendar produce queries (Zone 7a). |
| `ShoppingListService` | Generate and fetch shopping lists from meal plan gaps. |
| `ThemeService` | Dark/light mode toggle with `localStorage` persistence. |
| `WasteAlertService` | Fetch and dismiss waste alerts for expiring inventory items. |

## Models

TypeScript interfaces and enums for API request/response DTOs, organized by domain area under `models/`.
