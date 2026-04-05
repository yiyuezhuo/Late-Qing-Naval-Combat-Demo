
## Internal

Some UI Element's internal structure (can be derived from UI Toolkit Debugger), can be used to guide to override style, both for human and AI:

### `EnumField`

```
EnumField .unity-base-field .unity-base-field--no-label .unity-enum-field .topbar-field
    VisualElement .unity-base-field__input .unity-enum-field__input
        TextElement .unity-text-element .unity-enum-field__text
        VisualElement .unity-enum-field__arrow
```

If EnumField is stylized, then its Dyanmic created Dropdown should be stylized as well:

#### Dyanmic created Dropdown

It is created when EnumField or DropdownField is clicked, and removed when an option is selected or "outer" area is clicked. The internal looks like:

```
VisualElement .unity-base-dropdown
    VisualElement .unity-base-dropdown__container-outer
        ScrollView .unity-scroll-view .unity-base-dropdown__container-inner .unity-scroll-view--scroll .unity-scroll-view--vertical-horizontal
```

Dynamic created Dropdown would has some elements in `#unity-content-container` of ScrollView:

```
VisualElement .unity-base-dropdown__item
    VisualElement .unity-base-dropdown__item-content
        VisualElement .unity-base-dropdown__checkmark
        Label .unity-text-element .unity-label .unity-base-dropdown__label
```

If Dynamic created Dropdown is stylized, then those items should be stylized as well.

##### Apply Styles (Actionable)
When restyling this UI, always apply dropdown styling to:
- `.unity-base-dropdown__container-inner` (for background, it's default color is white)
- If Dynamic created Dropdown is stylized, then its ScrollView must be stylized.
Do NOT style top node `.unity-base-dropdown` (it's an element covering full screen to capture "outer" click, so it should stick to its default transparent style). 

### `ScrollView`

```
ScrollView .unity-scroll-view .unity-base-dropdown__container-inner .unity-scroll-view--scroll .unity-scroll-view--vertical-horizontal
    VisualElement #unity-content-and-vertical-scroll-container .unity-scroll-view__content-and-vertical-scroll-container
        VisualElement #unity-content-viewport .unity-scroll-view__content-viewport .unity-scroll-view__content-viewport--vertical-horizontal
            VisualElement #unity-content-container .unity-scroll-view__content-container .unity-scroll-view__content-container--vertical-horizontal
        Scroller .unity-scroller .unity-scroller--vertical .unity-scroll-view__vertical-scroller .unity-disabled
    Scroller .unity-scroller .unity-scroller--horizontal .unity-scroll-view__horizontal-scroller
```
If `ScrollView` is stylized, then its two `Scroller` must be stylized.

### `Scroller`

```
Scroller .unity-scroller .unity-scroller--vertical .unity-scroll-view__vertical-scroller .unity-disabled
    RepeatButton #unity-low-button .unity-text-element .unity-repeat-button .unity-scroller__low-button
    RepeatButton #unity-high-button .unity-text-element .unity-repeat-button .unity-scroller__high-button
    ScrollerSlider #unity-slider .unity-base-field .unity-base-field--no-label .unity-base-slider .unity-base-slider--vertical .unity-slider .unity-scroller__slider
        VisualElement .unity-base-field__input .unity-base-slider__input .unity-slider__input
            VisualElement #unity-drag-container .unity-base-slider__drag-container
                VisualElement #unity-tracker .unity-base-slider__tracker
                VisualElement #unity-dragger-border .unity-base-slider__dragger-border
                VisualElement #unity-dragger .unity-base-slider__dragger
```

Notes for styling of `Scroller`
- The button color of `RepeatButton` is controlled by `background-color` (default is RGB(240, 240, 240) from Unity default stylesheet)
- The arrow in the `RepeatButton` is controlled by `-unity-background-image-tint-color` (default is inline RGB(50, 50, 50))
- `RepeatButton` should be selected by `.unity-scroller > .unity-scroller__low-button, .unity-scroller > .unity-scroller__high-button`.
- The color of move area of the slider box is selected by `.unity-base-slider__drag-container > unity-base-slider__tracker`, (default is RGB(188, 188, 188) from Unity default stylesheet).
- The color of slider box is controlled by `.unity-base-slider__drag-container > unity-base-slider__dragger` (default is RGB(231, 231, 231) from Unity default stylesheet).


### `Toggle`

```
Toggle .unity-base-field .unity-base-field--no-label .unity-toggle
    VisualElement .unity-base-field__input .unity-toggle__input
        VisualElement #unity-checkmark .unity-toggle__checkmark
```

If `Toggle` is stylized, then its Check Mark must be stylized as well.

Check Mark (×) in the Toggle or selected item in the dropdown field can be controlled by `VisualElement.unity-toggle__checkmark`. The background color is the `background-color` (default is RGB(72,76,72)), the cross shape is controlled by `-unity-background-image-tint-color` (default is RGB(50,50,50)).

### `ProgressBar`

```
ProgressBar .unity-progress-bar .stat-bar
    VisualElement #unity-progress-bar .unity-progress-bar__container
        VisualElement .unity-progress-bar__background
            VisualElement .unity-progress-bar__progress
        VisualElement .unity-progress-bar__title-container
            Label .unity-text-element .unity-label .unity-progress-bar__title
```

### `TextField`

```
TextField .unity-base-field .unity-base-text-field .unity-text-field
    Label .unity-text-element .unity-label .unity-base-field__label .unity-base-text-field__label .unity-text-field__label
    TextInput #unity-text-input .unity-base-text-field__input .unity-base-text-field__input--single-line .unity-base-field__input .unity-text-field__input
        TextElement .unity-text-element .unity-text-element--selectable .unity-text-element--inner-input-field-component
```

## `IntegerField`

```
IntegerField .unity-base-field .unity-base-text-field .unity-integer-field
    Label .unity-text-element .unity-label .unity-base-field__label .unity-base-text-field__label .unity-integer-field__label .unity-base-field__label--with-dragger
    IntegerInput #unity-text-input .unity-base-text-field__input .unity-base-text-field__input--single-line .unity-base-field__input .unity-integer-field__input
```

## `TabView`

3 tabs examples:

```
TabView .unity-tab-view
  VisualElement .unity-tab-view__content-viewport
    VisualElement #unity-tab-view__header-container .unity-tab-view__header-container
      VisualElement #unity-tab__header .unity-tab__header
      VisualElement #unity-tab__header .unity-tab__header
      VisualElement #unity-tab__header .unity-tab__header
      RepeatButton .unity-text-element .unity-repeat-button .unity-tab-view__next-button
      RepeatButton .unity-text-element .unity-repeat-button .unity-tab-view__previous-button
    TabViewContentContainer #unity-tab-view__content-container .unity-tab-view__content-container
      Tab .unity-tab
      Tab .unity-tab
      Tab .unity-tab
```

It is recommended that there be no border or color difference between the header and the content container, so they appear visually connected.

### `TabView` Header

```
VisualElement #unity-tab__header .unity-tab__header
  Image #unity-tab__header-image .unity-image .unity-tab__header-image .unity-tab__header-image--empty
  Label #unity-tab__header-label .unity-text-element .unity-label .unity-tab__header-label
  VisualElement #unity-tab__header-underline .unity-tab__header-underline
```

Header's color is defined in `background-color` of `.unity-tab__header`. The default color is white.

Selected tab header can be selected by `.unity-tab__header:checked`

### `TabViewer` content container

```
Tab .unity-tab
  VisualElement #unity-tab__content-container .unity-tab__content-container
```

Content is the children of the container.

If border-less effect is wanted, `.unity-tab` should have `border-top-width` to be 0px.

## `RadioButton`

```
RadioButton .unity-base-field .unity-radio-button
  Label .unity-text-element .unity-label .unity-base-field__label .unity-radio-button__label
  VisualElement .unity-base-field__input .unity-radio-button__input
```

## `ListView`

```
ListView .unity-collection-view .unity-list-view .unity-list-view--with-footer
  ScrollView .unity-scroll-view .unity-scroll-view--scroll .unity-scroll-view--vertical .unity-collection-view__scroll-view .unity-list-view__scroll-view--with-footer
  VisualElement #unity-list-view__footer .unity-list-view__footer
    Button #unity-list-view__add-button .unity-text-element .unity-button
    Button #unity-list-view__remove-button .unity-text-element .unity-button
```

The `.unity-button` selector alone is not sufficient to override the styles defined in the default stylesheet. Consider using `#unity-list-view__add-button` and `#unity-list-view__remove-button` to specifically target these elements.

It's recommended that the `.unity-collection-view` itself have no background color (i.e., remain transparent). Instead, apply the same non-transparent background color to both `.unity-scroll-view` and `.unity-list-view__footer`, and ensure there is no border (0px) between them, so they appear visually connected.

## `MultiColumnListView`

3 columns example:

```
MultiColumnListView #MountStatusMultiColumnListView .unity-collection-view .unity-list-view .unity-list-view--with-footer
  ScrollView .unity-scroll-view .unity-scroll-view--scroll .unity-scroll-view--vertical .unity-collection-view__scroll-view .unity-list-view__scroll-view--with-footer
  VisualElement #unity-multi-column-view__header-container .unity-multi-column-view__header-container
    MultiColumnCollectionHeader .unity-multi-column-header
      VisualElement .unity-multi-column-header__column-container
        MultiColumnHeaderColumn .unity-multi-column-header__column
        MultiColumnHeaderColumn .unity-multi-column-header__column
        MultiColumnHeaderColumn .unity-multi-column-header__column
      VisualElement .unity-multi-column-header__resize-handle-container
  VisualElement #unity-content-and-vertical-scroll-container .unity-scroll-view__content-and-vertical-scroll-container
  Scroller .unity-scroller .unity-scroller--horizontal .unity-scroll-view__horizontal-scroller .unity-disabled
  VisualElement #unity-list-view__footer .unity-list-view__footer
    Button #unity-list-view__add-button .unity-text-element .unity-button
    Button #unity-list-view__remove-button .unity-text-element .unity-button
```

### `MultiColumnHeaderColumn`

```
MultiColumnHeaderColumn #mountLocationRecordSummary .unity-multi-column-header__column
  MultiColumnHeaderColumnSortIndicator .unity-multi-column-header__column__sort-indicator
  VisualElement .unity-multi-column-header__column__content-container
    TemplateContainer .unity-multi-column-header__column__content
      Label .unity-text-element .unity-label
```

Column header's box color is controlled by `background-color` of `.unity-multi-column-header__column` (default: RGB(188, 188, 188)).

## Default style

- Text color in the default style is black. So if a dark color background override is applied, a lighter color should applied to text.

## Unity UI Toolkit Style Rules (USS)

### Hover selector

To select hover, usually enabled should be added as well, for example:

```css
.unity-button:hover:enabled {
    background-color: green;
}
```

### Use variable

Never hardcode colors (rgb(...), rgba(...), #...) in USS except inside :root (the token file).

In USS, colors must be written as var(--cw-...).

If you need a new color, add a new token to :root and use var(...) everywhere else.

Treat hardcoded colors as a “lint error”.

Use semantic variable name instead of "palette"-style variable name.


### 🚫 Forbidden CSS properties

This project uses **Unity UI Toolkit (USS)** — NOT web CSS.

The syntax looks similar to CSS but many CSS properties are NOT supported.

The AI MUST follow the rules below when generating any `.uss` file.

----

The following web CSS properties DO NOT exist in USS and MUST NEVER be generated:

- box-shadow

If shadow or glow is needed, use **background images or gradients instead**.

## Binding Rule

- Unity UI Toolkit runtime data binding in this project does not reliably bind to plain auto-properties like `public bool foo { get; set; }`.
- For bindable model state, prefer `public` fields.
- If a type must expose an auto-property for interface/API reasons, add a separate `[CreateProperty]` wrapper in the adaption layer and bind UXML to that wrapper instead of the auto-property itself.
- When adding or reviewing bindings, always verify the `data-source-path` points to either a field or a `[CreateProperty]` property.
