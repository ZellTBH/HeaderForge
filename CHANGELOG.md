# Changelog

All notable changes to Header Forge are documented here.

## [0.1.0]

First release. The two header features are extracted from Sanguinis into a
package, with no behaviour change.

### Added

- **Category banners in the Inspector.** `ComponentSeparator`, an empty component
  whose custom editor draws a full width colored banner in its place, with a
  context menu for `Edit`, `Move Up`, `Move Down` and `Remove Header`.

- **Gradient separator bars in the Hierarchy.** GameObjects named `#RRGGBB=LABEL`
  or `#RRGGBB>#RRGGBB=LABEL` are drawn as colored bars. Nothing is serialized,
  the naming is the data.

### Changed

- Assemblies renamed from `InspectorHeader.Runtime`, `InspectorHeader.Editor` and
  `HierarchyHeader.Editor` to `BreakingFrame.HeaderForge.Runtime` and
  `BreakingFrame.HeaderForge.Editor`. The three previous assemblies become two:
  the Hierarchy drawer had its own, and it had no reason to.

- The `SessionState` key prefix moves from `InspectorHeader.Editing.` to
  `HeaderForge.Editing.`. Editing states already stored under the old prefix are
  dropped on the next launch, which is without consequence: the state only says
  whether a banner is currently expanded for editing.

### Preserved

- The GUID of `ComponentSeparator.cs` travels with the file. Existing instances
  keep their link and do not turn into missing scripts.

### Known limitations

- The banner hides the default grey component header by walking one level up the
  visual tree, which is internal Unity structure and free to change in any future
  version. Every step of that search is guarded and a failure does nothing at all,
  so the worst case is the default header showing above the banner rather than an
  exception on every repaint.
