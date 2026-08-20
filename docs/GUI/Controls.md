---
title: Custom Controls
aliases:
  - Controls
  - UI Components
tags:
  - gui
  - controls
---

# Custom Controls

Reusable UI components in the GUI.

## Control List

### VerificationButton

Displays verification status with appropriate color and icon.

| Status | Color | Icon |
|--------|-------|------|
| Verified | Green | Shield checkmark |
| Unverified | Yellow | Warning |
| Malicious | Red | Danger |
| Unreached | Grey | Question |

**Usage:**
```xml
<controls:VerificationButton Status="Verified" Playbook="{Binding Playbook}" />
```

### Spinner

Animated loading indicator for async operations.

**Properties:**
- `IsActive` (bool) - Start/stop animation
- `Width` / `Height` - Size

**Usage:**
```xml
<controls:Spinner IsActive="{Binding IsLoading}" Width="32" Height="32" />
```

### ProgressBarDeterminate

Standard progress bar with percentage display.

**Properties:**
- `Value` (double) - Current progress
- `Maximum` (double) - Maximum value

### ProgressBarIndeterminate

Animated progress bar for unknown-duration operations.

**Properties:**
- `IsActive` (bool) - Start/stop animation

### TextListItem

List item with icon and text.

**Properties:**
- `Icon` (string) - Icon name
- `Text` (string) - Display text

### TextDropDownBox

Dropdown selection with text display.

**Properties:**
- `Items` (IEnumerable) - Available items
- `SelectedItem` - Selected value

### LicensePropertyControl

Displays license information.

**Properties:**
- `PropertyName` - Property label
- `PropertyValue` - Property value

### PatreonModel

Patreon support/donation component.

## Page Components

### SelectISOPage
ISO file selection interface.

### SelectISOPane
ISO options container.

### FeaturePage
Individual feature page display.

### FeaturesPane
Feature pages container.

### RadioImageButton
Image-based radio button with gradient.

**Properties:**
- `GradientTopColor` - Top gradient color
- `GradientBottomColor` - Bottom gradient color
- `ImageSource` - Background image
- `IsSelected` - Selection state

### IsoFeaturePage
ISO-specific feature page.

### IsoFeaturePage
ISO feature container.

### IsoOptionPage
ISO option page.

### IsoOptionsPane
ISO options container.

### UsbProgressItem
USB write progress item.

### RadioPlaybookButton
Playbook selection button for ISO mode.

### MessageBox
Custom message box dialog.

## Icon Resources

The GUI includes extensive icon sets:

### Common Icons
- `shield_blue_gradient` - Security/trust
- `checkmark_green_gradient` - Success/verified
- `warning_circle_yellow_gradient` - Warning
- `clipboard_blue_gradient` - Tasks
- `code_blue_gradient` - Development

### Theme-Specific Icons
Located in `Icons/dark/` and `Icons/light/`:
- `ameliorated_logo` - Brand logo
- `checklist` - Task list
- `danger_sign` - Danger
- `external_link` - External links
- `question_circle` - Help
- `refresh` - Refresh
- `shield_checkmark` - Verified
- `shield_dots` - Processing
- `trash` - Delete
- `vendor_box` - Software
- `web` - Website

---

> [!info] See Also
> - [[GUI/Pages]] - Page documentation
> - [[GUI/Themes]] - Theme system
