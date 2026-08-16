# Header Forge

Two ways to cut visual noise in the Unity editor, for Breaking Frame productions.

Unity 6000.0 or later. Editor only in practice: the one runtime type is an empty
component that exists so the Inspector has a line to draw on.

## What it does

**Category banners in the Inspector.** A GameObject carrying a dozen components
reads as one undifferentiated stack. `ComponentSeparator` is an empty component
whose custom editor draws a full width colored banner in its place, so the stack
can be grouped under headings. Several are expected on the same GameObject.

**Gradient separator bars in the Hierarchy.** A GameObject named with the pattern
below is drawn as a colored bar instead of a normal row, which turns an empty
GameObject into a scene section header. Nothing is serialized: the naming IS the
data.

## Usage

### Inspector

Add the component through `Add Component`, then `Breaking Frame / Inspector
Header`. Set the label and the two gradient ends. Leaving both colors equal gives
a flat bar, so no toggle is needed to express one.

Click the banner to open its context menu: `Edit`, `Move Up`, `Move Down`,
`Remove Header`. The editing state lives in `SessionState`, so toggling it never
dirties the prefab and it resets when the editor restarts.

Arrange separators on the prefab asset, not on a scene instance. Reordering a
component on an instance records an ordering override that does not travel back
to the prefab and leaves the two out of sync.

### Hierarchy

Name an empty GameObject with one of these two forms:

```
#RRGGBB=LABEL              flat bar
#RRGGBB>#RRGGBB=LABEL      horizontal gradient
```

For example `#2E3340>#1253E5=LEVEL`. Any other name is left to Unity's default
drawing, and a regular name that happens to start with a hash is never mistaken
for a header: the whole pattern has to match.

Parsed names are cached per instance id and rebuilt only when the hierarchy
changes, so nothing is parsed and nothing is allocated while the window repaints.
Selection, renaming and drag and drop keep working, because only the repaint pass
is overridden and no event is ever consumed.

## Install

Add this to the `dependencies` block of your project's `Packages/manifest.json`:

```json
"com.breakingframe.headerforge": "https://github.com/ZellTBH/HeaderForge.git"
```

The repository is public, so no credential is needed. Unity resolves a git
dependency through your local git, which has to be installed and on the PATH.

## Notes

`ComponentSeparator` ships in builds. It is empty and costs nothing at runtime,
while stripping it conditionally would break the reference and show a missing
script on the prefab.
