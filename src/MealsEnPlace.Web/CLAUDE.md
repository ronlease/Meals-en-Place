# Web conventions

- Layout: `src/app/core` (singletons, interceptors), `src/app/shared` (reusable components), `src/app/features/<slice>` (components, service, routes, README).
- Standalone components only. No NgModules. Use `inject()`, signals, typed forms and typed `HttpClient` calls.
- Model slice to copy: `features/waste-alerts`. A new slice has `<name>-page.component.ts` and `.spec.ts` (loading and error states), `<name>.service.ts`, `<name>.routes.ts` with an entry in `app.routes.ts`, a README and a nav entry when it has a page. Folder names are kebab-case.
- Feature services own API calls. No cross-feature service imports.
- The frontend never converts units. It shows what the API returns.
- Show loading and error states (`mat-progress-spinner`, `mat-snack-bar`). Never leave a blank screen or an empty chart without a message.
- Unresolved recipes show an "Awaiting Resolution" badge and route to the resolution flow, not the detail view.
- Never bind untrusted HTML. CI blocks `innerHTML`, `bypassSecurityTrust`, `eval` and `new Function`.
- Prettier config is `.prettierrc` (100 columns, single quotes). CI checks it.
- Tests: Vitest through `ng test`. Coverage gate is in `angular.json`.
