# MealsEnPlace.Web

Angular 22 frontend for Meals en Place. Standalone components throughout — no NgModules.
The app is zoneless: there is no `zone.js` dependency and change detection runs on signals.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4280/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

Tests run on [Vitest](https://vitest.dev/) through Angular's first-party
`@angular/build:unit-test` builder, configured as the `test` target in `angular.json`.
There is no `vitest.config.ts` — the builder derives the Vitest configuration from that
target. Specs live beside the code they cover as `*.spec.ts`.

```bash
npm test                 # run every spec once
npm run test:coverage    # run with coverage and enforce the threshold
npx ng test              # watch mode in a TTY
```

```bash
# Run one spec file's suite by name
npx ng test --no-watch --filter="InventoryDialog"
```

### Coverage

The `test` target enforces **90% line coverage** via `coverageThresholds` in
`angular.json`, so `npm run test:coverage` fails the same way locally and in the
`Angular Test & Coverage` CI job. The HTML report lands in `coverage/` (gitignored).

Excluded from coverage: `main.ts`, `environments/`, `app.config.ts`, the `*.routes.ts`
files, and `core/models/` — bootstrap, configuration, and type-only declarations.

### Writing specs

Two conventions are worth knowing before adding a spec:

- **Components that import `MatDialogModule` or `MatSnackBarModule` need
  `TestBed.overrideProvider`.** Those modules supply their own `MatDialog` and
  `MatSnackBar`, which outrank a plain entry in TestBed's `providers` array.
- **Material buttons include the icon ligature in `textContent`.** Match a button label
  with `textContent.includes('Save')`, not an equality check against the trimmed text.

## Running end-to-end tests

No end-to-end framework is configured. `ng e2e` will prompt to install one.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
