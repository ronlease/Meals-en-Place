# Meal Plan

Weekly meal plan board with generation and recipe swap dialogs.

## Backlog

- MEP-007 Meal Plan Generation

## Route

`/meal-plan`

## Components

- **MealPlanBoardComponent** — 7-column grid (days of week) with meal slots as cards. Click a slot to swap.
- **MealPlanGenerateDialogComponent** — Form with plan name, seasonal preference checkbox.
- **MealPlanSwapDialogComponent** — Recipe selection list filtered to exclude current and unresolved recipes.

## Services Used

- `MealPlanService` — Get active plan, generate, swap slot
- `RecipeService` — Load recipes for swap dialog
