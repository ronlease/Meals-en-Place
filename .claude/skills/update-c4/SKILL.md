---
name: update-c4
description: Update the PlantUML C4 diagrams in docs/c4 and regenerate the committed PNGs. Use when a change adds or removes a container, component, external system or relationship, or when render-c4.sh --check fails.
---

# Update C4

1. Read the affected files in `docs/c4/` (`context.puml`, `container.puml`, `component-api.puml`, `component-web.puml`) and the code they describe. Keep diagrams true to what is built, not planned.
2. Edit the `.puml` files. Use C4-PlantUML macros. Do not show `Canning`, `StoreSales` or `Coupons` until they are built. External systems today: Claude API and Todoist. Add Open Food Facts only when a client exists.
3. Make sure Docker is running, then run `./scripts/render-c4.sh`. It pulls a pinned PlantUML image and needs network access for the C4-PlantUML include.
4. Run `./scripts/render-c4.sh --check`. It must pass.
5. Commit the `.puml` and the regenerated `.png` files together. The root `README.md` embeds the PNGs.

Never edit a PNG by hand. Permissions deny reads of `docs/c4/*.png`; open the `.puml` instead.
