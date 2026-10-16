/* @ds-bundle: {"format":4,"namespace":"RASSAngularMaterial_6477fb","components":[{"name":"Badge","sourcePath":"components/badge/Badge.jsx"},{"name":"Button","sourcePath":"components/button/Button.jsx"},{"name":"ButtonToggleGroup","sourcePath":"components/button/ButtonToggleGroup.jsx"},{"name":"Fab","sourcePath":"components/button/Fab.jsx"},{"name":"IconButton","sourcePath":"components/button/IconButton.jsx"},{"name":"Card","sourcePath":"components/card/Card.jsx"},{"name":"Chip","sourcePath":"components/chips/Chip.jsx"},{"name":"ChipSet","sourcePath":"components/chips/Chip.jsx"},{"name":"Accordion","sourcePath":"components/expansion/ExpansionPanel.jsx"},{"name":"ExpansionPanel","sourcePath":"components/expansion/ExpansionPanel.jsx"},{"name":"Autocomplete","sourcePath":"components/form-field/Autocomplete.jsx"},{"name":"Calendar","sourcePath":"components/form-field/Datepicker.jsx"},{"name":"Datepicker","sourcePath":"components/form-field/Datepicker.jsx"},{"name":"FormField","sourcePath":"components/form-field/FormField.jsx"},{"name":"Select","sourcePath":"components/form-field/Select.jsx"},{"name":"Divider","sourcePath":"components/icon/Divider.jsx"},{"name":"Icon","sourcePath":"components/icon/Icon.jsx"},{"name":"List","sourcePath":"components/list/List.jsx"},{"name":"ListItem","sourcePath":"components/list/List.jsx"},{"name":"Menu","sourcePath":"components/menu/Menu.jsx"},{"name":"BottomSheet","sourcePath":"components/overlay/BottomSheet.jsx"},{"name":"Dialog","sourcePath":"components/overlay/Dialog.jsx"},{"name":"Snackbar","sourcePath":"components/overlay/Snackbar.jsx"},{"name":"Tooltip","sourcePath":"components/overlay/Tooltip.jsx"},{"name":"ProgressBar","sourcePath":"components/progress/ProgressBar.jsx"},{"name":"ProgressSpinner","sourcePath":"components/progress/ProgressBar.jsx"},{"name":"Checkbox","sourcePath":"components/selection/Checkbox.jsx"},{"name":"RadioGroup","sourcePath":"components/selection/Radio.jsx"},{"name":"Radio","sourcePath":"components/selection/Radio.jsx"},{"name":"SlideToggle","sourcePath":"components/selection/SlideToggle.jsx"},{"name":"Sidenav","sourcePath":"components/sidenav/Sidenav.jsx"},{"name":"NavSection","sourcePath":"components/sidenav/Sidenav.jsx"},{"name":"NavItem","sourcePath":"components/sidenav/Sidenav.jsx"},{"name":"Slider","sourcePath":"components/slider/Slider.jsx"},{"name":"Stepper","sourcePath":"components/stepper/Stepper.jsx"},{"name":"Paginator","sourcePath":"components/table/Paginator.jsx"},{"name":"Table","sourcePath":"components/table/Table.jsx"},{"name":"Tabs","sourcePath":"components/tabs/Tabs.jsx"},{"name":"Toolbar","sourcePath":"components/toolbar/Toolbar.jsx"}],"sourceHashes":{"components/badge/Badge.jsx":"2edea9821d38","components/button/Button.jsx":"68b1451a0c73","components/button/ButtonToggleGroup.jsx":"b3552f0731af","components/button/Fab.jsx":"cf6d20213a17","components/button/IconButton.jsx":"8e0f9445e779","components/card/Card.jsx":"759ebcac819f","components/chips/Chip.jsx":"4c5d817b7f37","components/expansion/ExpansionPanel.jsx":"4d6a4c9c36e1","components/form-field/Autocomplete.jsx":"c2b57f054ee4","components/form-field/Datepicker.jsx":"722f5ac4ec94","components/form-field/FormField.jsx":"ad92d901e1eb","components/form-field/Select.jsx":"3e895ee303d9","components/icon/Divider.jsx":"ea1779b98605","components/icon/Icon.jsx":"abb853d8a015","components/list/List.jsx":"b754f7f3e88d","components/menu/Menu.jsx":"297765544c5a","components/overlay/BottomSheet.jsx":"c04948859edb","components/overlay/Dialog.jsx":"5b3c1bd77875","components/overlay/Snackbar.jsx":"d834f05277b2","components/overlay/Tooltip.jsx":"f55716693fc5","components/progress/ProgressBar.jsx":"d5a76292621b","components/selection/Checkbox.jsx":"756189393b57","components/selection/Radio.jsx":"7b6ae750220c","components/selection/SlideToggle.jsx":"ad72a01cb533","components/sidenav/Sidenav.jsx":"4463ff8a300e","components/slider/Slider.jsx":"6b1700dd8258","components/stepper/Stepper.jsx":"6e1557579327","components/table/Paginator.jsx":"06f2e47d0010","components/table/Table.jsx":"8a3d90b68ad1","components/tabs/Tabs.jsx":"fd087723c670","components/toolbar/Toolbar.jsx":"ad13bdfd186e","ui_kits/back-office/AppShell.jsx":"745e6314e46c","ui_kits/back-office/CustomerDetail.jsx":"7b2ff1651768","ui_kits/back-office/Customers.jsx":"767a13250836","ui_kits/back-office/Dashboard.jsx":"f21912a41494","ui_kits/back-office/NewCustomer.jsx":"d214d4068c6b","ui_kits/back-office/data.js":"e07fee1236b0"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.RASSAngularMaterial_6477fb = window.RASSAngularMaterial_6477fb || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/badge/Badge.jsx
try { (() => {
function Badge({
  content,
  size = 'medium',
  position = 'above after',
  overlap = true,
  hidden = false,
  inline = false,
  description,
  children,
  style
}) {
  const [v, h] = position.split(' ');
  const show = !hidden && (size === 'small' || content !== undefined && content !== null && content !== '');
  const txt = typeof content === 'number' && content > 999 ? '999+' : content;
  const cls = ['rass-badge', 'rass-badge--' + size, 'rass-badge--' + (v || 'above'), 'rass-badge--' + (h || 'after'), overlap && 'rass-badge--overlap', inline && 'rass-badge--inline'].filter(Boolean).join(' ');
  if (inline) return show ? /*#__PURE__*/React.createElement("span", {
    className: cls,
    style: style,
    "aria-label": description
  }, size !== 'small' && txt) : null;
  return /*#__PURE__*/React.createElement("span", {
    className: "rass-badge-host",
    style: style
  }, children, show && /*#__PURE__*/React.createElement("span", {
    className: cls,
    "aria-label": description
  }, size !== 'small' && txt));
}
Object.assign(__ds_scope, { Badge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/badge/Badge.jsx", error: String((e && e.message) || e) }); }

// components/card/Card.jsx
try { (() => {
function Card({
  appearance = 'raised',
  title,
  subtitle,
  avatar,
  headerAction,
  media,
  mediaHeight = 160,
  children,
  actions,
  actionsAlign = 'start',
  onClick,
  style,
  contentStyle
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-card rass-card--' + appearance + (onClick ? ' rass-card--clickable rass-sl' : ''),
    onClick: onClick,
    style: style
  }, (title || subtitle || avatar) && /*#__PURE__*/React.createElement("div", {
    className: "rass-card__header"
  }, avatar && /*#__PURE__*/React.createElement("div", {
    className: "rass-card__avatar"
  }, avatar), /*#__PURE__*/React.createElement("div", {
    className: "rass-card__titles"
  }, title && /*#__PURE__*/React.createElement("h3", {
    className: "rass-card__title"
  }, title), subtitle && /*#__PURE__*/React.createElement("p", {
    className: "rass-card__subtitle"
  }, subtitle)), headerAction), media && /*#__PURE__*/React.createElement("div", {
    className: "rass-card__media",
    style: {
      height: mediaHeight
    }
  }, media), children !== undefined && children !== null && /*#__PURE__*/React.createElement("div", {
    className: "rass-card__content",
    style: contentStyle
  }, children), actions && /*#__PURE__*/React.createElement("div", {
    className: 'rass-card__actions' + (actionsAlign === 'end' ? ' rass-card__actions--end' : '')
  }, actions));
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/card/Card.jsx", error: String((e && e.message) || e) }); }

// components/icon/Divider.jsx
try { (() => {
function Divider({
  vertical = false,
  inset = false,
  style
}) {
  return /*#__PURE__*/React.createElement("hr", {
    role: "separator",
    "aria-orientation": vertical ? 'vertical' : 'horizontal',
    className: 'rass-divider' + (vertical ? ' rass-divider--vertical' : '') + (inset ? ' rass-divider--inset' : ''),
    style: style
  });
}
Object.assign(__ds_scope, { Divider });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/icon/Divider.jsx", error: String((e && e.message) || e) }); }

// components/icon/Icon.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Icon({
  name,
  size = 24,
  fill = false,
  className = '',
  style,
  ...rest
}) {
  const sz = size === 18 || size === 'sm' ? ' rass-icon--sm' : size === 36 || size === 'lg' ? ' rass-icon--lg' : '';
  const s = typeof size === 'number' && size !== 18 && size !== 24 && size !== 36 ? {
    fontSize: size,
    ...style
  } : style;
  return /*#__PURE__*/React.createElement("span", _extends({
    className: 'rass-icon' + sz + (className ? ' ' + className : ''),
    "data-fill": fill ? '1' : '0',
    "aria-hidden": "true",
    style: s
  }, rest), name);
}
Object.assign(__ds_scope, { Icon });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/icon/Icon.jsx", error: String((e && e.message) || e) }); }

// components/button/Button.jsx
try { (() => {
function Button({
  variant = 'text',
  icon,
  trailingIcon,
  color,
  disabled = false,
  state,
  type = 'button',
  onClick,
  children,
  style,
  className = ''
}) {
  const cls = ['rass-btn', 'rass-sl', 'rass-focus-ring', 'rass-btn--' + variant, icon ? 'rass-btn--with-icon' : '', trailingIcon ? 'rass-btn--trailing-icon' : '', color === 'warn' || color === 'error' ? 'rass-btn--danger' : '', className].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("button", {
    type: type,
    className: cls,
    disabled: disabled,
    "data-state": state,
    onClick: onClick,
    style: style
  }, icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon
  }), /*#__PURE__*/React.createElement("span", null, children), trailingIcon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: trailingIcon
  }));
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/button/Button.jsx", error: String((e && e.message) || e) }); }

// components/button/ButtonToggleGroup.jsx
try { (() => {
function ButtonToggleGroup({
  options = [],
  value,
  defaultValue,
  multiple = false,
  hideSingleSelectionIndicator = false,
  disabled = false,
  onChange,
  ariaLabel,
  style
}) {
  const [inner, setInner] = React.useState(defaultValue ?? (multiple ? [] : null));
  const cur = value !== undefined ? value : inner;
  const isOn = v => multiple ? (cur || []).includes(v) : cur === v;
  const pick = v => {
    const next = multiple ? isOn(v) ? cur.filter(x => x !== v) : [...(cur || []), v] : v;
    if (value === undefined) setInner(next);
    onChange && onChange(next);
  };
  return /*#__PURE__*/React.createElement("div", {
    role: "group",
    "aria-label": ariaLabel,
    className: "rass-toggle-group",
    style: style
  }, options.map(o => {
    const opt = typeof o === 'string' ? {
      value: o,
      label: o
    } : o;
    const on = isOn(opt.value);
    return /*#__PURE__*/React.createElement("button", {
      key: opt.value,
      type: "button",
      className: "rass-toggle rass-sl",
      "aria-pressed": String(on),
      disabled: disabled || opt.disabled,
      onClick: () => pick(opt.value),
      "aria-label": opt.label ? undefined : opt.icon
    }, on && !hideSingleSelectionIndicator ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
      name: "check"
    }) : opt.icon ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
      name: opt.icon
    }) : null, opt.label && /*#__PURE__*/React.createElement("span", null, opt.label));
  }));
}
Object.assign(__ds_scope, { ButtonToggleGroup });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/button/ButtonToggleGroup.jsx", error: String((e && e.message) || e) }); }

// components/button/Fab.jsx
try { (() => {
function Fab({
  icon,
  label,
  size = 'regular',
  color = 'primary',
  disabled = false,
  state,
  ariaLabel,
  onClick,
  style
}) {
  const cls = ['rass-fab', 'rass-sl', 'rass-focus-ring', size !== 'regular' ? 'rass-fab--' + size : '', label ? 'rass-fab--extended' : '', color !== 'primary' ? 'rass-fab--' + color : ''].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: cls,
    disabled: disabled,
    "data-state": state,
    "aria-label": label ? undefined : ariaLabel || icon,
    onClick: onClick,
    style: style
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon
  }), label && /*#__PURE__*/React.createElement("span", null, label));
}
Object.assign(__ds_scope, { Fab });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/button/Fab.jsx", error: String((e && e.message) || e) }); }

// components/button/IconButton.jsx
try { (() => {
function IconButton({
  icon,
  variant = 'standard',
  selected,
  disabled = false,
  state,
  ariaLabel,
  onClick,
  style
}) {
  const toggle = selected !== undefined;
  const cls = 'rass-icon-btn rass-sl rass-focus-ring' + (variant !== 'standard' ? ' rass-icon-btn--' + variant : '');
  return /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: cls,
    disabled: disabled,
    "data-state": state,
    "aria-label": ariaLabel || icon,
    "aria-pressed": toggle ? String(!!selected) : undefined,
    onClick: onClick,
    style: style
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    fill: !!selected
  }));
}
Object.assign(__ds_scope, { IconButton });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/button/IconButton.jsx", error: String((e && e.message) || e) }); }

// components/chips/Chip.jsx
try { (() => {
function Chip({
  kind = 'assist',
  label,
  children,
  icon,
  avatar,
  selected,
  defaultSelected = false,
  removable,
  disabled = false,
  elevated = false,
  state,
  onClick,
  onRemove,
  onChange,
  style
}) {
  const [inner, setInner] = React.useState(defaultSelected);
  const isFilter = kind === 'filter';
  const sel = isFilter ? selected !== undefined ? selected : inner : false;
  const canRemove = removable ?? kind === 'input';
  const lead = isFilter && sel ? 'check' : icon;
  const cls = ['rass-chip', 'rass-sl', 'rass-focus-ring', 'rass-chip--' + kind, lead && !avatar && 'rass-chip--lead', avatar && 'rass-chip--avatar', canRemove && 'rass-chip--trail', elevated && 'rass-chip--elevated'].filter(Boolean).join(' ');
  const click = e => {
    if (isFilter) {
      if (selected === undefined) setInner(!sel);
      onChange && onChange(!sel);
    }
    onClick && onClick(e);
  };
  return /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: cls,
    disabled: disabled,
    "data-state": state,
    role: isFilter ? 'option' : undefined,
    "aria-selected": isFilter ? String(sel) : undefined,
    onClick: click,
    style: style
  }, avatar && /*#__PURE__*/React.createElement("span", {
    className: "rass-chip__avatar"
  }, avatar), lead && !avatar && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: lead,
    className: "rass-chip__lead"
  }), /*#__PURE__*/React.createElement("span", null, label ?? children), canRemove && /*#__PURE__*/React.createElement("span", {
    role: "button",
    tabIndex: 0,
    "aria-label": 'Remove ' + (label || ''),
    className: "rass-chip__remove",
    onClick: e => {
      e.stopPropagation();
      onRemove && onRemove();
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "cancel"
  })));
}
function ChipSet({
  children,
  ariaLabel,
  role = 'listbox',
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-chip-set",
    role: role,
    "aria-label": ariaLabel,
    style: style
  }, children);
}
Object.assign(__ds_scope, { Chip, ChipSet });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/chips/Chip.jsx", error: String((e && e.message) || e) }); }

// components/expansion/ExpansionPanel.jsx
try { (() => {
const AccCtx = React.createContext(null);
function Accordion({
  multi = false,
  displayMode = 'default',
  children,
  style
}) {
  const [open, setOpen] = React.useState(null);
  const ctx = multi ? null : {
    open,
    setOpen
  };
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-accordion",
    "data-display-mode": displayMode,
    style: style
  }, /*#__PURE__*/React.createElement(AccCtx.Provider, {
    value: ctx
  }, children));
}
function ExpansionPanel({
  title,
  description,
  expanded,
  defaultExpanded = false,
  disabled = false,
  hideToggle = false,
  flat = false,
  onToggle,
  actions,
  children,
  style
}) {
  const acc = React.useContext(AccCtx);
  const id = React.useId();
  const [inner, setInner] = React.useState(defaultExpanded);
  React.useEffect(() => {
    if (acc && defaultExpanded) acc.setOpen(id);
  }, []);
  const isOpen = expanded !== undefined ? expanded : acc ? acc.open === id : inner;
  const toggle = () => {
    if (disabled) return;
    if (expanded === undefined) {
      if (acc) acc.setOpen(isOpen ? null : id);else setInner(!isOpen);
    }
    onToggle && onToggle(!isOpen);
  };
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-xp' + (isOpen ? ' rass-xp--expanded' : '') + (disabled ? ' rass-xp--disabled' : '') + (flat ? ' rass-xp--flat' : ''),
    style: style
  }, /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "rass-xp__header rass-sl",
    "aria-expanded": isOpen,
    "aria-disabled": disabled || undefined,
    onClick: toggle
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-xp__title"
  }, title), /*#__PURE__*/React.createElement("span", {
    className: "rass-xp__desc"
  }, description), !hideToggle && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "expand_more",
    className: "rass-xp__ind"
  })), isOpen && /*#__PURE__*/React.createElement("div", {
    className: "rass-xp__body"
  }, children), isOpen && actions && /*#__PURE__*/React.createElement("div", {
    className: "rass-xp__actions"
  }, actions));
}
Object.assign(__ds_scope, { Accordion, ExpansionPanel });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/expansion/ExpansionPanel.jsx", error: String((e && e.message) || e) }); }

// components/form-field/FormField.jsx
try { (() => {
function FormField({
  label,
  hint,
  hintEnd,
  error,
  appearance = 'outline',
  value,
  defaultValue,
  placeholder,
  type = 'text',
  prefixIcon,
  suffixIcon,
  textPrefix,
  required = false,
  disabled = false,
  readOnly = false,
  multiline = false,
  rows = 3,
  state,
  focused: focusedProp,
  filled: filledProp,
  open = false,
  floatLabel = 'auto',
  block = false,
  hideSubscript = false,
  width,
  onChange,
  onFocus,
  onBlur,
  onClick,
  onKeyDown,
  suffix,
  children,
  inputRef,
  style,
  name,
  autoComplete
}) {
  const [inner, setInner] = React.useState(defaultValue ?? '');
  const [focus, setFocus] = React.useState(false);
  const v = value !== undefined ? value : inner;
  const isFocused = focusedProp ?? (focus || state === 'focus');
  const hasValue = filledProp ?? (v !== '' && v != null);
  const float = floatLabel === 'always' || isFocused || hasValue || !!placeholder && isFocused;
  const err = !!error;
  const cls = ['rass-ff', 'rass-ff--' + appearance, float && 'rass-ff--float', isFocused && 'rass-ff--focused', err && 'rass-ff--error', disabled && 'rass-ff--disabled', prefixIcon && 'rass-ff--prefix', multiline && 'rass-ff--textarea', open && 'rass-ff--open', block && 'rass-ff--block', hideSubscript && 'rass-ff--no-sub'].filter(Boolean).join(' ');
  const id = React.useId();
  const change = e => {
    if (value === undefined) setInner(e.target.value);
    onChange && onChange(e.target.value, e);
  };
  const Tag = multiline ? 'textarea' : 'input';
  return /*#__PURE__*/React.createElement("div", {
    className: cls,
    "data-state": state,
    style: {
      ...(width ? {
        width
      } : null),
      ...style
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "rass-ff__box",
    onClick: onClick
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__outline"
  }), label && /*#__PURE__*/React.createElement("label", {
    className: "rass-ff__label",
    htmlFor: id
  }, label, required && /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__req"
  }, " *")), prefixIcon && /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__prefix"
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: prefixIcon
  })), textPrefix && float && /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__text-prefix"
  }, textPrefix), children !== undefined ? children : /*#__PURE__*/React.createElement(Tag, {
    id: id,
    ref: inputRef,
    name: name,
    autoComplete: autoComplete,
    className: "rass-ff__input",
    type: multiline ? undefined : type,
    rows: multiline ? rows : undefined,
    value: v,
    placeholder: placeholder,
    disabled: disabled,
    readOnly: readOnly,
    required: required,
    "aria-invalid": err || undefined,
    "aria-describedby": id + '-sub',
    onChange: change,
    onKeyDown: onKeyDown,
    onFocus: e => {
      setFocus(true);
      onFocus && onFocus(e);
    },
    onBlur: e => {
      setFocus(false);
      onBlur && onBlur(e);
    }
  }), (suffixIcon || suffix || err) && /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__suffix"
  }, suffix, suffixIcon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: suffixIcon
  }), err && !suffixIcon && !suffix && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "error"
  }))), /*#__PURE__*/React.createElement("div", {
    className: "rass-ff__sub",
    id: id + '-sub'
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__sub-start"
  }, err ? /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__error",
    role: "alert"
  }, error) : hint), hintEnd && !err && /*#__PURE__*/React.createElement("span", null, hintEnd)));
}
Object.assign(__ds_scope, { FormField });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/form-field/FormField.jsx", error: String((e && e.message) || e) }); }

// components/form-field/Autocomplete.jsx
try { (() => {
function hl(text, q) {
  if (!q) return text;
  const i = text.toLowerCase().indexOf(q.toLowerCase());
  if (i < 0) return text;
  return /*#__PURE__*/React.createElement(React.Fragment, null, text.slice(0, i), /*#__PURE__*/React.createElement("mark", null, text.slice(i, i + q.length)), text.slice(i + q.length));
}
function Autocomplete({
  label,
  options = [],
  value,
  defaultValue,
  placeholder,
  hint,
  error,
  required,
  disabled,
  appearance = 'outline',
  prefixIcon = 'search',
  open: openProp,
  block,
  width,
  onChange,
  onSelect,
  style
}) {
  const [inner, setInner] = React.useState(defaultValue ?? '');
  const [focus, setFocus] = React.useState(false);
  const [active, setActive] = React.useState(0);
  const q = value !== undefined ? value : inner;
  const list = options.filter(o => !q || o.toLowerCase().includes(q.toLowerCase())).slice(0, 8);
  const open = openProp ?? (focus && list.length > 0 && !(list.length === 1 && list[0] === q));
  const set = v => {
    if (value === undefined) setInner(v);
    onChange && onChange(v);
  };
  const choose = v => {
    set(v);
    onSelect && onSelect(v);
    setFocus(false);
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: block ? 'block' : 'inline-block',
      width: block ? '100%' : width,
      ...style
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.FormField, {
    label: label,
    hint: hint,
    error: error,
    required: required,
    disabled: disabled,
    appearance: appearance,
    block: block || !!width,
    prefixIcon: prefixIcon,
    placeholder: placeholder,
    value: q,
    focused: openProp ? true : undefined,
    onChange: v => {
      set(v);
      setActive(0);
    },
    onFocus: () => setFocus(true),
    onBlur: () => setTimeout(() => setFocus(false), 120),
    onKeyDown: e => {
      if (e.key === 'ArrowDown') setActive(a => Math.min(a + 1, list.length - 1));
      if (e.key === 'ArrowUp') setActive(a => Math.max(a - 1, 0));
      if (e.key === 'Enter' && list[active]) choose(list[active]);
    }
  }), open && /*#__PURE__*/React.createElement("div", {
    className: "rass-panel",
    role: "listbox"
  }, list.map((o, i) => /*#__PURE__*/React.createElement("div", {
    key: o,
    role: "option",
    "aria-selected": false,
    className: 'rass-option rass-sl' + (i === active ? ' rass-option--active' : ''),
    onMouseDown: () => choose(o)
  }, hl(o, q)))));
}
Object.assign(__ds_scope, { Autocomplete });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/form-field/Autocomplete.jsx", error: String((e && e.message) || e) }); }

// components/form-field/Datepicker.jsx
try { (() => {
const sameDay = (a, b) => a && b && a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
function Calendar({
  selected,
  onSelect,
  locale = 'en-US',
  today = new Date(),
  inline = false,
  startView
}) {
  const [view, setView] = React.useState(() => {
    const d = selected || today;
    return new Date(d.getFullYear(), d.getMonth(), 1);
  });
  const first = (new Intl.Locale(locale).weekInfo?.firstDay ?? (locale.startsWith('en-US') ? 7 : 1)) % 7;
  const dows = Array.from({
    length: 7
  }, (_, i) => new Date(2024, 0, 7 + (first + i) % 7).toLocaleDateString(locale, {
    weekday: 'narrow'
  }));
  const offset = (view.getDay() - first + 7) % 7;
  const days = new Date(view.getFullYear(), view.getMonth() + 1, 0).getDate();
  const cells = [...Array(offset).fill(null), ...Array.from({
    length: days
  }, (_, i) => new Date(view.getFullYear(), view.getMonth(), i + 1))];
  const move = n => setView(new Date(view.getFullYear(), view.getMonth() + n, 1));
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-calendar' + (inline ? ' rass-calendar--inline' : ''),
    role: "dialog",
    "aria-label": "Calendar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "rass-calendar__head"
  }, /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "rass-calendar__period rass-sl"
  }, view.toLocaleDateString(locale, {
    month: 'short',
    year: 'numeric'
  }).toUpperCase(), /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "arrow_drop_down",
    size: 18
  })), /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'flex'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "chevron_left",
    ariaLabel: "Previous month",
    onClick: () => move(-1)
  }), /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "chevron_right",
    ariaLabel: "Next month",
    onClick: () => move(1)
  }))), /*#__PURE__*/React.createElement("div", {
    className: "rass-calendar__grid"
  }, dows.map((d, i) => /*#__PURE__*/React.createElement("span", {
    key: 'd' + i,
    className: "rass-calendar__dow"
  }, d)), cells.map((d, i) => d ? /*#__PURE__*/React.createElement("button", {
    key: i,
    type: "button",
    className: 'rass-calendar__day rass-sl' + (sameDay(d, today) ? ' rass-calendar__day--today' : ''),
    "aria-selected": sameDay(d, selected) || undefined,
    onClick: () => onSelect && onSelect(d)
  }, d.getDate()) : /*#__PURE__*/React.createElement("span", {
    key: i
  }))));
}
function Datepicker({
  label = 'Choose a date',
  value,
  defaultValue,
  locale = 'en-US',
  hint,
  error,
  required,
  disabled,
  appearance = 'outline',
  open: openProp,
  block,
  width,
  onChange,
  today,
  style
}) {
  const [inner, setInner] = React.useState(defaultValue ?? null);
  const [openS, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  const open = openProp ?? openS;
  const cur = value !== undefined ? value : inner;
  const fmt = d => d ? d.toLocaleDateString(locale, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }) : '';
  React.useEffect(() => {
    if (!openS) return;
    const h = e => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', h);
    return () => document.removeEventListener('mousedown', h);
  }, [openS]);
  return /*#__PURE__*/React.createElement("div", {
    ref: ref,
    style: {
      position: 'relative',
      display: block ? 'block' : 'inline-block',
      width: block ? '100%' : width,
      ...style
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.FormField, {
    label: label,
    hint: hint ?? (locale.startsWith('en-US') ? 'MM/DD/YYYY' : 'DD/MM/YYYY'),
    error: error,
    required: required,
    disabled: disabled,
    appearance: appearance,
    block: block || !!width,
    value: fmt(cur),
    readOnly: true,
    focused: open || undefined,
    onClick: () => !disabled && setOpen(true),
    suffix: /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
      icon: "calendar_today",
      ariaLabel: "Open calendar",
      disabled: disabled,
      onClick: e => {
        e.stopPropagation();
        setOpen(!open);
      }
    })
  }), open && /*#__PURE__*/React.createElement(Calendar, {
    selected: cur,
    locale: locale,
    today: today,
    onSelect: d => {
      if (value === undefined) setInner(d);
      onChange && onChange(d);
      setOpen(false);
    }
  }));
}
Object.assign(__ds_scope, { Calendar, Datepicker });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/form-field/Datepicker.jsx", error: String((e && e.message) || e) }); }

// components/form-field/Select.jsx
try { (() => {
function Select({
  label,
  options = [],
  value,
  defaultValue,
  multiple = false,
  placeholder,
  hint,
  error,
  required,
  disabled,
  appearance = 'outline',
  open: openProp,
  block,
  width,
  prefixIcon,
  onChange,
  state,
  style
}) {
  const [inner, setInner] = React.useState(defaultValue ?? (multiple ? [] : ''));
  const [openS, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  const open = openProp ?? openS;
  const cur = value !== undefined ? value : inner;
  const opts = options.map(o => typeof o === 'string' ? {
    value: o,
    label: o
  } : o);
  const isSel = v => multiple ? (cur || []).includes(v) : cur === v;
  const text = multiple ? opts.filter(o => isSel(o.value)).map(o => o.label).join(', ') : (opts.find(o => o.value === cur) || {}).label || '';
  React.useEffect(() => {
    if (!openS) return;
    const h = e => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', h);
    return () => document.removeEventListener('mousedown', h);
  }, [openS]);
  const pick = v => {
    const next = multiple ? isSel(v) ? cur.filter(x => x !== v) : [...cur, v] : v;
    if (value === undefined) setInner(next);
    onChange && onChange(next);
    if (!multiple) setOpen(false);
  };
  return /*#__PURE__*/React.createElement("div", {
    ref: ref,
    style: {
      position: 'relative',
      display: block ? 'block' : 'inline-block',
      width: block ? '100%' : width,
      ...style
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.FormField, {
    label: label,
    hint: hint,
    error: error,
    required: required,
    disabled: disabled,
    appearance: appearance,
    block: block || !!width,
    width: width ? '100%' : undefined,
    prefixIcon: prefixIcon,
    state: state,
    focused: open || state === 'focus',
    filled: !!text || open && !!placeholder,
    open: open,
    onClick: () => !disabled && setOpen(!open),
    suffix: /*#__PURE__*/React.createElement(__ds_scope.Icon, {
      name: "arrow_drop_down",
      className: "rass-select-arrow"
    })
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-ff__input rass-select-value",
    role: "combobox",
    "aria-expanded": open,
    tabIndex: disabled ? -1 : 0,
    style: {
      cursor: disabled ? 'default' : 'pointer',
      color: !text ? 'var(--mat-sys-on-surface-variant)' : undefined
    },
    onKeyDown: e => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        setOpen(!open);
      }
      if (e.key === 'Escape') setOpen(false);
    }
  }, text || (open || !label ? placeholder : ''))), open && /*#__PURE__*/React.createElement("div", {
    className: "rass-panel",
    role: "listbox",
    "aria-multiselectable": multiple || undefined
  }, opts.map(o => /*#__PURE__*/React.createElement("div", {
    key: o.value,
    role: "option",
    "aria-selected": isSel(o.value),
    "aria-disabled": o.disabled || undefined,
    className: "rass-option rass-sl",
    onClick: () => pick(o.value)
  }, multiple && /*#__PURE__*/React.createElement("span", {
    className: "rass-pseudo-check",
    "data-checked": isSel(o.value)
  }, isSel(o.value) && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "check"
  })), /*#__PURE__*/React.createElement("span", {
    style: {
      flex: 1
    }
  }, o.label), !multiple && isSel(o.value) && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "check",
    size: 18
  })))));
}
Object.assign(__ds_scope, { Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/form-field/Select.jsx", error: String((e && e.message) || e) }); }

// components/list/List.jsx
try { (() => {
function List({
  subheader,
  children,
  ariaLabel,
  role = 'list',
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-list",
    role: role,
    "aria-label": ariaLabel,
    style: style
  }, subheader && /*#__PURE__*/React.createElement("div", {
    className: "rass-list__subheader"
  }, subheader), children);
}
function ListItem({
  headline,
  supportingText,
  lines,
  leadingIcon,
  avatar,
  leading,
  trailingText,
  trailingIcon,
  trailing,
  selected,
  disabled = false,
  onClick,
  href,
  state,
  style
}) {
  const n = lines || (supportingText ? String(supportingText).length > 60 ? 3 : 2 : 1);
  const interactive = !!(onClick || href);
  const cls = ['rass-li', n === 2 && 'rass-li--two', n === 3 && 'rass-li--three', interactive && 'rass-li--interactive rass-sl', disabled && 'rass-li--disabled'].filter(Boolean).join(' ');
  const Tag = href ? 'a' : interactive ? 'button' : 'div';
  return /*#__PURE__*/React.createElement(Tag, {
    className: cls,
    href: href,
    type: Tag === 'button' ? 'button' : undefined,
    role: Tag === 'div' ? 'listitem' : undefined,
    "aria-selected": selected !== undefined ? selected : undefined,
    "data-state": state,
    onClick: onClick,
    style: style
  }, leading, leadingIcon && /*#__PURE__*/React.createElement("span", {
    className: "rass-li__lead"
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: leadingIcon,
    fill: !!selected
  })), avatar && /*#__PURE__*/React.createElement("span", {
    className: "rass-li__avatar"
  }, avatar), /*#__PURE__*/React.createElement("span", {
    className: "rass-li__text"
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-li__headline"
  }, headline), supportingText && /*#__PURE__*/React.createElement("span", {
    className: "rass-li__support"
  }, supportingText)), (trailingText || trailingIcon || trailing) && /*#__PURE__*/React.createElement("span", {
    className: "rass-li__trail"
  }, trailingText, trailingIcon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: trailingIcon
  }), trailing));
}
Object.assign(__ds_scope, { List, ListItem });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/list/List.jsx", error: String((e && e.message) || e) }); }

// components/menu/Menu.jsx
try { (() => {
function Menu({
  items = [],
  trigger,
  open: openProp,
  inline = false,
  position = 'below-after',
  onSelect,
  onClose,
  style
}) {
  const [openS, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  const open = inline || (openProp ?? openS);
  React.useEffect(() => {
    if (!openS) return;
    const h = e => {
      if (ref.current && !ref.current.contains(e.target)) {
        setOpen(false);
        onClose && onClose();
      }
    };
    const k = e => {
      if (e.key === 'Escape') {
        setOpen(false);
        onClose && onClose();
      }
    };
    document.addEventListener('mousedown', h);
    document.addEventListener('keydown', k);
    return () => {
      document.removeEventListener('mousedown', h);
      document.removeEventListener('keydown', k);
    };
  }, [openS]);
  const panel = open && /*#__PURE__*/React.createElement("div", {
    className: 'rass-menu ' + (inline ? 'rass-menu--inline' : 'rass-menu--' + position),
    role: "menu",
    style: inline ? style : undefined
  }, items.map((it, i) => it.divider ? /*#__PURE__*/React.createElement("div", {
    key: i,
    className: "rass-menu__divider",
    role: "separator"
  }) : /*#__PURE__*/React.createElement("button", {
    key: i,
    type: "button",
    role: "menuitem",
    disabled: it.disabled,
    "data-state": it.state,
    className: 'rass-menu__item rass-sl' + (it.danger ? ' rass-menu__item--danger' : ''),
    onClick: () => {
      onSelect && onSelect(it.value ?? it.label);
      it.onClick && it.onClick();
      setOpen(false);
    }
  }, it.icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: it.icon
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-menu__item-label"
  }, it.label), it.trailingText && /*#__PURE__*/React.createElement("span", {
    className: "rass-menu__trail"
  }, it.trailingText), it.submenu && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "arrow_right"
  }))));
  if (inline) return panel;
  return /*#__PURE__*/React.createElement("span", {
    ref: ref,
    className: "rass-menu-anchor",
    style: style
  }, /*#__PURE__*/React.createElement("span", {
    onClick: () => setOpen(!openS),
    "aria-haspopup": "menu",
    "aria-expanded": open
  }, trigger), panel);
}
Object.assign(__ds_scope, { Menu });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/menu/Menu.jsx", error: String((e && e.message) || e) }); }

// components/overlay/BottomSheet.jsx
try { (() => {
function BottomSheet({
  open = true,
  title,
  children,
  inline = false,
  local = false,
  onClose,
  style
}) {
  if (!open) return null;
  const sheet = /*#__PURE__*/React.createElement("div", {
    className: "rass-sheet",
    role: "dialog",
    "aria-label": typeof title === 'string' ? title : undefined,
    style: style,
    onClick: e => e.stopPropagation()
  }, title && /*#__PURE__*/React.createElement("div", {
    className: "rass-sheet__title"
  }, title), children);
  if (inline) return sheet;
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-overlay rass-overlay--bottom' + (local ? ' rass-overlay--local' : ''),
    onClick: onClose
  }, sheet);
}
Object.assign(__ds_scope, { BottomSheet });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/overlay/BottomSheet.jsx", error: String((e && e.message) || e) }); }

// components/overlay/Dialog.jsx
try { (() => {
function Dialog({
  open = true,
  title,
  icon,
  children,
  actions,
  width,
  divided = false,
  inline = false,
  local = false,
  disableClose = false,
  onClose,
  ariaLabel,
  style
}) {
  React.useEffect(() => {
    if (!open || inline || disableClose) return;
    const k = e => {
      if (e.key === 'Escape') onClose && onClose();
    };
    document.addEventListener('keydown', k);
    return () => document.removeEventListener('keydown', k);
  }, [open, inline, disableClose]);
  if (!open) return null;
  const box = /*#__PURE__*/React.createElement("div", {
    className: 'rass-dialog' + (divided ? ' rass-dialog--divided' : ''),
    role: "dialog",
    "aria-modal": !inline,
    "aria-label": ariaLabel || (typeof title === 'string' ? title : undefined),
    style: {
      width,
      ...style
    },
    onClick: e => e.stopPropagation()
  }, icon && /*#__PURE__*/React.createElement("div", {
    className: "rass-dialog__icon"
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon
  })), title && /*#__PURE__*/React.createElement("h2", {
    className: "rass-dialog__title"
  }, title), children !== undefined && /*#__PURE__*/React.createElement("div", {
    className: "rass-dialog__content"
  }, children), actions && /*#__PURE__*/React.createElement("div", {
    className: "rass-dialog__actions"
  }, actions));
  if (inline) return box;
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-overlay' + (local ? ' rass-overlay--local' : ''),
    onClick: disableClose ? undefined : onClose
  }, box);
}
Object.assign(__ds_scope, { Dialog });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/overlay/Dialog.jsx", error: String((e && e.message) || e) }); }

// components/overlay/Snackbar.jsx
try { (() => {
function Snackbar({
  open = true,
  message,
  action,
  onAction,
  dismissible = false,
  duration,
  inline = false,
  local = false,
  onClose,
  style
}) {
  React.useEffect(() => {
    if (!open || !duration) return;
    const t = setTimeout(() => onClose && onClose(), duration);
    return () => clearTimeout(t);
  }, [open, duration, message]);
  if (!open) return null;
  const bar = /*#__PURE__*/React.createElement("div", {
    className: "rass-snackbar",
    role: "status",
    "aria-live": "polite",
    style: inline ? style : undefined
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-snackbar__label"
  }, message), action && /*#__PURE__*/React.createElement(__ds_scope.Button, {
    variant: "text",
    onClick: () => {
      onAction && onAction();
      onClose && onClose();
    }
  }, action), dismissible && /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "close",
    ariaLabel: "Dismiss",
    onClick: onClose
  }));
  if (inline) return bar;
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-snackbar-host' + (local ? ' rass-snackbar-host--local' : ''),
    style: style
  }, bar);
}
Object.assign(__ds_scope, { Snackbar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/overlay/Snackbar.jsx", error: String((e && e.message) || e) }); }

// components/overlay/Tooltip.jsx
try { (() => {
function Tooltip({
  message,
  position = 'below',
  show,
  children,
  style
}) {
  const [hover, setHover] = React.useState(false);
  const vis = show ?? hover;
  return /*#__PURE__*/React.createElement("span", {
    className: "rass-tooltip-host",
    style: style,
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => setHover(false),
    onFocus: () => setHover(true),
    onBlur: () => setHover(false)
  }, children, /*#__PURE__*/React.createElement("span", {
    role: "tooltip",
    className: 'rass-tooltip rass-tooltip--' + position + (vis ? ' rass-tooltip--show' : '')
  }, message));
}
Object.assign(__ds_scope, { Tooltip });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/overlay/Tooltip.jsx", error: String((e && e.message) || e) }); }

// components/progress/ProgressBar.jsx
try { (() => {
function ProgressBar({
  mode = 'determinate',
  value = 0,
  bufferValue = 0,
  ariaLabel,
  style
}) {
  const det = mode === 'determinate' || mode === 'buffer';
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-progress rass-progress--' + mode,
    role: "progressbar",
    "aria-label": ariaLabel,
    "aria-valuemin": 0,
    "aria-valuemax": 100,
    "aria-valuenow": det ? value : undefined,
    style: style
  }, mode === 'buffer' && /*#__PURE__*/React.createElement("span", {
    className: "rass-progress__buffer",
    style: {
      width: bufferValue + '%'
    }
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-progress__bar",
    style: det ? {
      width: value + '%'
    } : undefined
  }));
}
function ProgressSpinner({
  mode = 'indeterminate',
  value = 0,
  diameter = 48,
  strokeWidth = 4,
  ariaLabel,
  style
}) {
  const r = (diameter - strokeWidth) / 2;
  const c = 2 * Math.PI * r;
  return /*#__PURE__*/React.createElement("span", {
    className: 'rass-spinner rass-spinner--' + mode,
    role: "progressbar",
    "aria-label": ariaLabel,
    "aria-valuenow": mode === 'determinate' ? value : undefined,
    style: {
      width: diameter,
      height: diameter,
      ...style
    }
  }, /*#__PURE__*/React.createElement("svg", {
    width: diameter,
    height: diameter,
    viewBox: '0 0 ' + diameter + ' ' + diameter,
    style: {
      display: 'block',
      transform: mode === 'determinate' ? 'rotate(-90deg)' : undefined
    }
  }, /*#__PURE__*/React.createElement("circle", {
    cx: diameter / 2,
    cy: diameter / 2,
    r: r,
    strokeWidth: strokeWidth,
    style: mode === 'determinate' ? {
      strokeDasharray: c,
      strokeDashoffset: c * (1 - value / 100)
    } : {
      transformOrigin: 'center'
    }
  })));
}
Object.assign(__ds_scope, { ProgressBar, ProgressSpinner });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/progress/ProgressBar.jsx", error: String((e && e.message) || e) }); }

// components/selection/Checkbox.jsx
try { (() => {
function Checkbox({
  checked,
  defaultChecked = false,
  indeterminate = false,
  disabled = false,
  error = false,
  labelPosition = 'after',
  state,
  onChange,
  children,
  ariaLabel,
  style
}) {
  const [inner, setInner] = React.useState(defaultChecked);
  const on = checked !== undefined ? checked : inner;
  const filled = on || indeterminate;
  const cls = ['rass-sel', labelPosition === 'before' && 'rass-sel--before', filled && 'rass-sel--on', disabled && 'rass-sel--disabled', error && 'rass-sel--error'].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("label", {
    className: cls,
    style: style
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-sel__touch rass-sl",
    "data-state": state,
    "aria-disabled": disabled || undefined
  }, /*#__PURE__*/React.createElement("input", {
    type: "checkbox",
    className: "rass-sel__input",
    checked: on,
    disabled: disabled,
    "aria-label": ariaLabel,
    "aria-checked": indeterminate ? 'mixed' : on,
    onChange: e => {
      if (checked === undefined) setInner(e.target.checked);
      onChange && onChange(e.target.checked);
    }
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-sel__ring",
    style: state === 'focus' ? {
      outline: 'var(--rass-focus-ring-width) solid var(--rass-focus-ring-color)'
    } : undefined
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-check__box"
  }, indeterminate ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "remove"
  }) : on ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "check"
  }) : null)), children && /*#__PURE__*/React.createElement("span", null, children));
}
Object.assign(__ds_scope, { Checkbox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/selection/Checkbox.jsx", error: String((e && e.message) || e) }); }

// components/selection/Radio.jsx
try { (() => {
const RadioCtx = React.createContext(null);
function RadioGroup({
  value,
  defaultValue,
  name,
  direction = 'column',
  disabled = false,
  onChange,
  ariaLabel,
  children,
  style
}) {
  const [inner, setInner] = React.useState(defaultValue ?? null);
  const cur = value !== undefined ? value : inner;
  const auto = React.useId();
  const ctx = {
    value: cur,
    name: name || auto,
    disabled,
    select: v => {
      if (value === undefined) setInner(v);
      onChange && onChange(v);
    }
  };
  return /*#__PURE__*/React.createElement("div", {
    role: "radiogroup",
    "aria-label": ariaLabel,
    className: 'rass-radio-group' + (direction === 'row' ? ' rass-radio-group--row' : ''),
    style: style
  }, /*#__PURE__*/React.createElement(RadioCtx.Provider, {
    value: ctx
  }, children));
}
function Radio({
  value,
  checked,
  disabled,
  labelPosition = 'after',
  state,
  onChange,
  children,
  style
}) {
  const g = React.useContext(RadioCtx);
  const on = g ? g.value === value : !!checked;
  const dis = disabled ?? (g ? g.disabled : false);
  const cls = ['rass-sel', labelPosition === 'before' && 'rass-sel--before', on && 'rass-sel--on', dis && 'rass-sel--disabled'].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("label", {
    className: cls,
    style: style
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-sel__touch rass-sl",
    "data-state": state,
    "aria-disabled": dis || undefined
  }, /*#__PURE__*/React.createElement("input", {
    type: "radio",
    className: "rass-sel__input",
    name: g ? g.name : undefined,
    value: value,
    checked: on,
    disabled: dis,
    onChange: () => {
      g ? g.select(value) : onChange && onChange(value);
    }
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-sel__ring",
    style: state === 'focus' ? {
      outline: 'var(--rass-focus-ring-width) solid var(--rass-focus-ring-color)'
    } : undefined
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-radio__outer"
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-radio__dot"
  }))), children && /*#__PURE__*/React.createElement("span", null, children));
}
Object.assign(__ds_scope, { RadioGroup, Radio });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/selection/Radio.jsx", error: String((e && e.message) || e) }); }

// components/selection/SlideToggle.jsx
try { (() => {
function SlideToggle({
  checked,
  defaultChecked = false,
  disabled = false,
  hideIcon = false,
  labelPosition = 'after',
  state,
  onChange,
  children,
  ariaLabel,
  style
}) {
  const [inner, setInner] = React.useState(defaultChecked);
  const on = checked !== undefined ? checked : inner;
  const cls = ['rass-sel', 'rass-switch', labelPosition === 'before' && 'rass-sel--before', on && 'rass-switch--on', disabled && 'rass-sel--disabled rass-switch--disabled'].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("label", {
    className: cls,
    style: style
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'relative',
      display: 'inline-flex'
    }
  }, /*#__PURE__*/React.createElement("input", {
    type: "checkbox",
    role: "switch",
    className: "rass-sel__input",
    checked: on,
    disabled: disabled,
    "aria-label": ariaLabel,
    "aria-checked": on,
    onChange: e => {
      if (checked === undefined) setInner(e.target.checked);
      onChange && onChange(e.target.checked);
    }
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-switch__track",
    style: state === 'focus' ? {
      outline: 'var(--rass-focus-ring-width) solid var(--rass-focus-ring-color)',
      outlineOffset: 2
    } : undefined
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-switch__handle-wrap rass-sl",
    "data-state": state === 'focus' ? undefined : state,
    "aria-disabled": disabled || undefined
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-switch__handle"
  }, on && !hideIcon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "check"
  }))))), children && /*#__PURE__*/React.createElement("span", null, children));
}
Object.assign(__ds_scope, { SlideToggle });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/selection/SlideToggle.jsx", error: String((e && e.message) || e) }); }

// components/sidenav/Sidenav.jsx
try { (() => {
function Sidenav({
  mode = 'side',
  opened = true,
  collapsed = false,
  header,
  children,
  content,
  onClose,
  width,
  style,
  contentStyle
}) {
  const cls = ['rass-sidenav', 'rass-sidenav--' + mode, !opened && 'rass-sidenav--closed', collapsed && 'rass-sidenav--collapsed'].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-sidenav-container",
    style: style
  }, /*#__PURE__*/React.createElement("nav", {
    className: cls,
    style: width && !collapsed ? {
      width
    } : undefined
  }, header && /*#__PURE__*/React.createElement("div", {
    className: "rass-sidenav__header"
  }, header), children), mode === 'over' && opened && /*#__PURE__*/React.createElement("div", {
    className: "rass-sidenav__scrim",
    onClick: onClose
  }), /*#__PURE__*/React.createElement("div", {
    className: "rass-sidenav-content",
    style: contentStyle
  }, content));
}
function NavSection({
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-sidenav__section"
  }, children);
}
function NavItem({
  icon,
  label,
  activated = false,
  badge,
  meta,
  href,
  onClick,
  state
}) {
  const Tag = href ? 'a' : 'button';
  return /*#__PURE__*/React.createElement(Tag, {
    className: "rass-nav-item rass-sl",
    href: href,
    type: href ? undefined : 'button',
    "aria-current": activated ? 'page' : undefined,
    "data-state": state,
    onClick: onClick,
    title: label
  }, icon && (badge !== undefined ? /*#__PURE__*/React.createElement(__ds_scope.Badge, {
    content: badge,
    size: badge === true ? 'small' : 'medium'
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    fill: activated
  })) : /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    fill: activated
  })), /*#__PURE__*/React.createElement("span", {
    className: "rass-nav-item__label"
  }, label), meta !== undefined && /*#__PURE__*/React.createElement("span", {
    className: "rass-nav-item__meta"
  }, meta));
}
Object.assign(__ds_scope, { Sidenav, NavSection, NavItem });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/sidenav/Sidenav.jsx", error: String((e && e.message) || e) }); }

// components/slider/Slider.jsx
try { (() => {
function Slider({
  min = 0,
  max = 100,
  step = 1,
  value,
  defaultValue,
  discrete = false,
  showLabel = false,
  showTickMarks = false,
  disabled = false,
  block = false,
  width,
  displayWith,
  onChange,
  ariaLabel,
  style
}) {
  const [inner, setInner] = React.useState(defaultValue ?? min);
  const [drag, setDrag] = React.useState(false);
  const v = value !== undefined ? value : inner;
  const pct = (v - min) / (max - min) * 100;
  const ticks = showTickMarks ? Math.floor((max - min) / step) + 1 : 0;
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-slider' + (block ? ' rass-slider--block' : '') + (disabled ? ' rass-slider--disabled' : ''),
    style: {
      ...(width ? {
        width
      } : null),
      ...style
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-slider__track"
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-slider__active",
    style: {
      width: pct + '%'
    }
  }), Array.from({
    length: ticks
  }, (_, i) => {
    const p = i / (ticks - 1) * 100;
    return /*#__PURE__*/React.createElement("span", {
      key: i,
      className: 'rass-slider__tick' + (p <= pct ? ' rass-slider__tick--on' : ''),
      style: {
        left: p + '%'
      }
    });
  }), /*#__PURE__*/React.createElement("input", {
    className: "rass-slider__input",
    type: "range",
    min: min,
    max: max,
    step: step,
    value: v,
    disabled: disabled,
    "aria-label": ariaLabel,
    onPointerDown: () => setDrag(true),
    onPointerUp: () => setDrag(false),
    onBlur: () => setDrag(false),
    onChange: e => {
      const n = Number(e.target.value);
      if (value === undefined) setInner(n);
      onChange && onChange(n);
    }
  }), /*#__PURE__*/React.createElement("span", {
    className: "rass-slider__thumb-wrap",
    style: {
      left: pct + '%'
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-slider__thumb"
  }), discrete && (drag || showLabel) && /*#__PURE__*/React.createElement("span", {
    className: "rass-slider__label"
  }, displayWith ? displayWith(v) : v)));
}
Object.assign(__ds_scope, { Slider });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/slider/Slider.jsx", error: String((e && e.message) || e) }); }

// components/stepper/Stepper.jsx
try { (() => {
function StepHeader({
  step,
  index,
  selected,
  onClick,
  disabled
}) {
  const done = step.completed && !selected;
  const err = step.error;
  const cls = ['rass-step', 'rass-sl', selected && 'rass-step--selected', done && 'rass-step--done', err && 'rass-step--error'].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("button", {
    type: "button",
    role: "tab",
    "aria-selected": selected,
    "aria-disabled": disabled || undefined,
    className: cls,
    onClick: disabled ? undefined : onClick
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-step__icon"
  }, err ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "warning"
  }) : done ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: step.editable === false ? 'check' : 'edit'
  }) : index + 1), /*#__PURE__*/React.createElement("span", {
    className: "rass-step__labels"
  }, /*#__PURE__*/React.createElement("span", {
    className: "rass-step__label"
  }, step.label), (step.optional || err) && /*#__PURE__*/React.createElement("span", {
    className: "rass-step__optional"
  }, typeof err === 'string' ? err : 'Optional')));
}
function Stepper({
  steps = [],
  selectedIndex,
  defaultIndex = 0,
  orientation = 'horizontal',
  linear = false,
  onChange,
  style
}) {
  const [inner, setInner] = React.useState(defaultIndex);
  const cur = selectedIndex !== undefined ? selectedIndex : inner;
  const go = i => {
    if (selectedIndex === undefined) setInner(i);
    onChange && onChange(i);
  };
  const blocked = i => linear && i > cur && steps.slice(0, i).some(s => !s.completed);
  if (orientation === 'vertical') {
    return /*#__PURE__*/React.createElement("div", {
      className: "rass-stepper rass-stepper--vertical",
      role: "tablist",
      "aria-orientation": "vertical",
      style: style
    }, steps.map((s, i) => /*#__PURE__*/React.createElement("div", {
      key: i,
      className: "rass-stepper__vstep"
    }, /*#__PURE__*/React.createElement(StepHeader, {
      step: s,
      index: i,
      selected: i === cur,
      disabled: blocked(i),
      onClick: () => go(i)
    }), /*#__PURE__*/React.createElement("div", {
      className: "rass-stepper__vbody"
    }, i === cur ? s.content : null))));
  }
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-stepper",
    style: style
  }, /*#__PURE__*/React.createElement("div", {
    className: "rass-stepper__header",
    role: "tablist"
  }, steps.map((s, i) => /*#__PURE__*/React.createElement(React.Fragment, {
    key: i
  }, i > 0 && /*#__PURE__*/React.createElement("span", {
    className: "rass-stepper__line"
  }), /*#__PURE__*/React.createElement(StepHeader, {
    step: s,
    index: i,
    selected: i === cur,
    disabled: blocked(i),
    onClick: () => go(i)
  })))), steps[cur] && steps[cur].content !== undefined && /*#__PURE__*/React.createElement("div", {
    className: "rass-stepper__content",
    role: "tabpanel"
  }, steps[cur].content));
}
Object.assign(__ds_scope, { Stepper });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/stepper/Stepper.jsx", error: String((e && e.message) || e) }); }

// components/table/Paginator.jsx
try { (() => {
function Paginator({
  length = 0,
  pageSize: psProp,
  pageIndex: piProp,
  defaultPageSize = 10,
  pageSizeOptions = [10, 25, 50],
  showFirstLastButtons = true,
  hidePageSize = false,
  itemsPerPageLabel = 'Items per page:',
  ofLabel = 'of',
  onPage,
  style
}) {
  const [psS, setPs] = React.useState(defaultPageSize);
  const [piS, setPi] = React.useState(0);
  const ps = psProp ?? psS;
  const pi = piProp ?? piS;
  const pages = Math.max(1, Math.ceil(length / ps));
  const go = (i, size = ps) => {
    if (piProp === undefined) setPi(i);
    if (psProp === undefined) setPs(size);
    onPage && onPage({
      pageIndex: i,
      pageSize: size,
      length
    });
  };
  const start = length === 0 ? 0 : pi * ps + 1;
  const end = Math.min(length, (pi + 1) * ps);
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-paginator",
    style: style
  }, !hidePageSize && /*#__PURE__*/React.createElement("div", {
    className: "rass-paginator__size"
  }, /*#__PURE__*/React.createElement("span", null, itemsPerPageLabel), /*#__PURE__*/React.createElement(__ds_scope.Select, {
    options: pageSizeOptions.map(n => ({
      value: n,
      label: String(n)
    })),
    value: ps,
    onChange: n => go(0, n)
  })), /*#__PURE__*/React.createElement("span", {
    className: "rass-paginator__range"
  }, start, " \u2013 ", end, " ", ofLabel, " ", length), /*#__PURE__*/React.createElement("div", {
    className: "rass-paginator__nav"
  }, showFirstLastButtons && /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "first_page",
    ariaLabel: "First page",
    disabled: pi === 0,
    onClick: () => go(0)
  }), /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "chevron_left",
    ariaLabel: "Previous page",
    disabled: pi === 0,
    onClick: () => go(pi - 1)
  }), /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "chevron_right",
    ariaLabel: "Next page",
    disabled: pi >= pages - 1,
    onClick: () => go(pi + 1)
  }), showFirstLastButtons && /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "last_page",
    ariaLabel: "Last page",
    disabled: pi >= pages - 1,
    onClick: () => go(pages - 1)
  })));
}
Object.assign(__ds_scope, { Paginator });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/table/Paginator.jsx", error: String((e && e.message) || e) }); }

// components/table/Table.jsx
try { (() => {
function Table({
  columns = [],
  rows = [],
  rowKey = 'id',
  sort: sortProp,
  defaultSort,
  onSortChange,
  selectable = false,
  selected: selProp,
  onSelectionChange,
  onRowClick,
  stickyHeader = false,
  emptyText = 'No data',
  rowActions,
  maxHeight,
  ariaLabel,
  style
}) {
  const [sortS, setSort] = React.useState(defaultSort || null);
  const [selS, setSel] = React.useState([]);
  const sort = sortProp !== undefined ? sortProp : sortS;
  const sel = selProp !== undefined ? selProp : selS;
  const setSelection = n => {
    if (selProp === undefined) setSel(n);
    onSelectionChange && onSelectionChange(n);
  };
  const toggleSort = key => {
    const next = !sort || sort.active !== key ? {
      active: key,
      direction: 'asc'
    } : sort.direction === 'asc' ? {
      active: key,
      direction: 'desc'
    } : null;
    if (sortProp === undefined) setSort(next);
    onSortChange && onSortChange(next);
  };
  let data = rows;
  if (sort && !onSortChange) {
    const col = columns.find(c => c.key === sort.active);
    const get = r => col && col.sortValue ? col.sortValue(r) : r[sort.active];
    data = [...rows].sort((a, b) => {
      const x = get(a),
        y = get(b);
      const c = typeof x === 'number' ? x - y : String(x).localeCompare(String(y));
      return sort.direction === 'asc' ? c : -c;
    });
  }
  const keys = data.map(r => r[rowKey]);
  const all = keys.length > 0 && keys.every(k => sel.includes(k));
  const some = !all && keys.some(k => sel.includes(k));
  return /*#__PURE__*/React.createElement("div", {
    className: "rass-table-wrap",
    style: {
      maxHeight,
      ...style
    }
  }, /*#__PURE__*/React.createElement("table", {
    className: 'rass-table' + (stickyHeader ? ' rass-table--sticky' : ''),
    "aria-label": ariaLabel
  }, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, selectable && /*#__PURE__*/React.createElement("th", {
    className: "rass-cell--check"
  }, /*#__PURE__*/React.createElement(__ds_scope.Checkbox, {
    checked: all,
    indeterminate: some,
    ariaLabel: "Select all",
    onChange: () => setSelection(all ? [] : keys)
  })), columns.map(c => {
    const active = sort && sort.active === c.key;
    return /*#__PURE__*/React.createElement("th", {
      key: c.key,
      className: c.numeric ? 'rass-num' : undefined,
      style: {
        width: c.width
      },
      "aria-sort": active ? sort.direction === 'asc' ? 'ascending' : 'descending' : undefined
    }, c.sortable ? /*#__PURE__*/React.createElement("button", {
      type: "button",
      className: "rass-sort",
      "aria-sort": active ? sort.direction : undefined,
      onClick: () => toggleSort(c.key)
    }, /*#__PURE__*/React.createElement("span", null, c.header), /*#__PURE__*/React.createElement(__ds_scope.Icon, {
      name: active && sort.direction === 'desc' ? 'arrow_downward' : 'arrow_upward'
    })) : c.header);
  }), rowActions && /*#__PURE__*/React.createElement("th", {
    className: "rass-cell--actions"
  }))), /*#__PURE__*/React.createElement("tbody", null, data.length === 0 && /*#__PURE__*/React.createElement("tr", {
    className: "rass-table__empty"
  }, /*#__PURE__*/React.createElement("td", {
    colSpan: columns.length + (selectable ? 1 : 0) + (rowActions ? 1 : 0)
  }, emptyText)), data.map(r => {
    const k = r[rowKey];
    const isSel = sel.includes(k);
    return /*#__PURE__*/React.createElement("tr", {
      key: k,
      "aria-selected": selectable ? isSel : undefined,
      className: onRowClick ? 'rass-row--click' : undefined,
      onClick: onRowClick ? () => onRowClick(r) : undefined
    }, selectable && /*#__PURE__*/React.createElement("td", {
      className: "rass-cell--check",
      onClick: e => e.stopPropagation()
    }, /*#__PURE__*/React.createElement(__ds_scope.Checkbox, {
      checked: isSel,
      ariaLabel: 'Select row ' + k,
      onChange: () => setSelection(isSel ? sel.filter(x => x !== k) : [...sel, k])
    })), columns.map(c => /*#__PURE__*/React.createElement("td", {
      key: c.key,
      className: c.numeric ? 'rass-num' : undefined
    }, c.render ? c.render(r) : r[c.key])), rowActions && /*#__PURE__*/React.createElement("td", {
      className: "rass-cell--actions",
      onClick: e => e.stopPropagation()
    }, rowActions(r)));
  }))));
}
Object.assign(__ds_scope, { Table });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/table/Table.jsx", error: String((e && e.message) || e) }); }

// components/tabs/Tabs.jsx
try { (() => {
function Tabs({
  tabs = [],
  selectedIndex,
  defaultIndex = 0,
  stretch = false,
  iconPosition = 'start',
  nav = false,
  onChange,
  bodyStyle,
  style,
  ariaLabel
}) {
  const [inner, setInner] = React.useState(defaultIndex);
  const cur = selectedIndex !== undefined ? selectedIndex : inner;
  const pick = i => {
    if (selectedIndex === undefined) setInner(i);
    onChange && onChange(i);
  };
  const active = tabs[cur];
  return /*#__PURE__*/React.createElement("div", {
    className: 'rass-tabs' + (stretch ? ' rass-tabs--stretch' : ''),
    style: style
  }, /*#__PURE__*/React.createElement("div", {
    className: "rass-tabs__header",
    role: nav ? 'navigation' : 'tablist',
    "aria-label": ariaLabel
  }, tabs.map((t, i) => /*#__PURE__*/React.createElement("button", {
    key: i,
    type: "button",
    role: nav ? 'link' : 'tab',
    "aria-selected": i === cur,
    "aria-current": nav && i === cur ? 'page' : undefined,
    disabled: t.disabled,
    className: 'rass-tab rass-sl' + (t.icon && iconPosition === 'top' ? ' rass-tab--icon-top' : ''),
    onClick: () => pick(i)
  }, t.icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: t.icon,
    fill: i === cur
  }), /*#__PURE__*/React.createElement("span", null, t.label), t.badge !== undefined && /*#__PURE__*/React.createElement(__ds_scope.Badge, {
    inline: true,
    content: t.badge
  })))), !nav && active && active.content !== undefined && /*#__PURE__*/React.createElement("div", {
    className: "rass-tabs__body",
    role: "tabpanel",
    style: bodyStyle
  }, active.content));
}
Object.assign(__ds_scope, { Tabs });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/tabs/Tabs.jsx", error: String((e && e.message) || e) }); }

// components/toolbar/Toolbar.jsx
try { (() => {
function Toolbar({
  title,
  leading,
  children,
  actions,
  color = 'surface',
  scrolled = false,
  divider = false,
  style
}) {
  const cls = ['rass-toolbar', color !== 'surface' && 'rass-toolbar--' + color, scrolled && 'rass-toolbar--scrolled', divider && 'rass-toolbar--divider'].filter(Boolean).join(' ');
  return /*#__PURE__*/React.createElement("header", {
    className: cls,
    role: "toolbar",
    style: style
  }, leading, title && /*#__PURE__*/React.createElement("span", {
    className: "rass-toolbar__title"
  }, title), children, /*#__PURE__*/React.createElement("span", {
    className: "rass-toolbar__spacer"
  }), actions && /*#__PURE__*/React.createElement("div", {
    className: "rass-toolbar__actions"
  }, actions));
}
Object.assign(__ds_scope, { Toolbar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/toolbar/Toolbar.jsx", error: String((e && e.message) || e) }); }

// ui_kits/back-office/AppShell.jsx
try { (() => {
// App shell: mat-sidenav-container + mat-toolbar. Exposes AppShell on window.
(function () {
  const {
    Sidenav,
    NavItem,
    NavSection,
    Toolbar,
    IconButton,
    Badge,
    Menu,
    Tooltip,
    Icon
  } = window.RASSAngularMaterial_6477fb;
  function AppShell({
    route,
    go,
    dark,
    setDark,
    toolbarTitle,
    children
  }) {
    const [collapsed, setCollapsed] = React.useState(false);
    const nav = [{
      id: 'dashboard',
      icon: 'dashboard',
      label: 'Panel'
    }, {
      id: 'customers',
      icon: 'group',
      label: 'Clientes',
      match: ['customers', 'customer', 'new']
    }, {
      id: 'orders',
      icon: 'receipt_long',
      label: 'Pedidos',
      badge: 4
    }, {
      id: 'invoices',
      icon: 'request_quote',
      label: 'Facturas'
    }];
    const admin = [{
      id: 'products',
      icon: 'inventory_2',
      label: 'Productos'
    }, {
      id: 'reports',
      icon: 'bar_chart',
      label: 'Informes'
    }, {
      id: 'settings',
      icon: 'settings',
      label: 'Ajustes'
    }];
    const item = n => /*#__PURE__*/React.createElement(NavItem, {
      key: n.id,
      icon: n.icon,
      label: n.label,
      badge: n.badge,
      activated: (n.match || [n.id]).includes(route),
      onClick: () => go(n.id)
    });
    return /*#__PURE__*/React.createElement(Sidenav, {
      collapsed: collapsed,
      style: {
        height: '100vh'
      },
      contentStyle: {
        background: 'var(--mat-sys-surface)'
      },
      header: collapsed ? null : /*#__PURE__*/React.createElement("span", {
        style: {
          font: 'var(--mat-sys-title-medium)',
          color: 'var(--mat-sys-on-surface)',
          letterSpacing: 0
        }
      }, "RASS"),
      content: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(Toolbar, {
        leading: /*#__PURE__*/React.createElement(IconButton, {
          icon: collapsed ? 'menu' : 'menu_open',
          ariaLabel: "Alternar men\xFA",
          onClick: () => setCollapsed(!collapsed)
        }),
        title: toolbarTitle,
        actions: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(Tooltip, {
          message: dark ? 'Tema claro' : 'Tema oscuro'
        }, /*#__PURE__*/React.createElement(IconButton, {
          icon: dark ? 'light_mode' : 'dark_mode',
          ariaLabel: "Cambiar tema",
          onClick: () => setDark(!dark)
        })), /*#__PURE__*/React.createElement(Tooltip, {
          message: "Notificaciones"
        }, /*#__PURE__*/React.createElement(Badge, {
          content: 2,
          description: "2 notificaciones"
        }, /*#__PURE__*/React.createElement(IconButton, {
          icon: "notifications",
          ariaLabel: "Notificaciones"
        }))), /*#__PURE__*/React.createElement(Menu, {
          position: "below-before",
          trigger: /*#__PURE__*/React.createElement(IconButton, {
            icon: "account_circle",
            ariaLabel: "Cuenta"
          }),
          items: [{
            icon: 'person',
            label: 'Mi perfil'
          }, {
            icon: 'help',
            label: 'Ayuda'
          }, {
            divider: true
          }, {
            icon: 'logout',
            label: 'Cerrar sesión'
          }]
        }))
      }), /*#__PURE__*/React.createElement("main", {
        style: {
          flex: 1,
          minHeight: 0
        }
      }, children))
    }, nav.map(item), /*#__PURE__*/React.createElement(NavSection, null, "Administraci\xF3n"), admin.map(item));
  }
  window.AppShell = AppShell;
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/back-office/AppShell.jsx", error: String((e && e.message) || e) }); }

// ui_kits/back-office/CustomerDetail.jsx
try { (() => {
// Customer detail: page header, tabs, form sections in cards / expansion panels.
(function () {
  const {
    Tabs,
    Card,
    FormField,
    Select,
    Datepicker,
    Autocomplete,
    SlideToggle,
    Checkbox,
    RadioGroup,
    Radio,
    Accordion,
    ExpansionPanel,
    Button,
    IconButton,
    Chip,
    ChipSet,
    Table,
    List,
    ListItem,
    Slider,
    Icon,
    Menu
  } = window.RASSAngularMaterial_6477fb;
  const {
    eur
  } = window.RASS_FMT;
  const {
    orders
  } = window.RASS_DATA;
  const cities = ['Alicante', 'Barcelona', 'Bilbao', 'Castellón', 'Córdoba', 'Madrid', 'Málaga', 'Salamanca', 'Sevilla', 'Valencia', 'Valladolid', 'Zaragoza'];
  function General({
    c,
    dirty
  }) {
    const [nif, setNif] = React.useState(c.nif);
    const nifErr = nif && !/^[A-Z0-9]{9}$/.test(nif) ? 'El NIF debe tener 9 caracteres' : null;
    const grid = {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fill,minmax(260px,1fr))',
      columnGap: 16,
      rowGap: 4
    };
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0,2fr) minmax(280px,1fr)',
        gap: 16,
        alignItems: 'start'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 16
      }
    }, /*#__PURE__*/React.createElement(Card, {
      appearance: "outlined",
      title: "Datos generales",
      contentStyle: {
        paddingTop: 16
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: grid
    }, /*#__PURE__*/React.createElement(FormField, {
      label: "Raz\xF3n social",
      defaultValue: c.name,
      required: true,
      block: true,
      onChange: dirty
    }), /*#__PURE__*/React.createElement(FormField, {
      label: "NIF / CIF",
      value: nif,
      onChange: v => {
        setNif(v.toUpperCase());
        dirty();
      },
      required: true,
      error: nifErr,
      hint: "Sin guiones ni espacios",
      block: true
    }), /*#__PURE__*/React.createElement(Select, {
      label: "Segmento",
      defaultValue: c.segment,
      options: ['Autónomo', 'Pyme', 'Empresa'],
      block: true,
      onChange: dirty
    }), /*#__PURE__*/React.createElement(Datepicker, {
      label: "Cliente desde",
      locale: "es-ES",
      defaultValue: new Date(c.since),
      block: true,
      onChange: dirty
    }), /*#__PURE__*/React.createElement(FormField, {
      label: "Persona de contacto",
      defaultValue: c.contact,
      prefixIcon: "person",
      block: true,
      onChange: dirty
    }), /*#__PURE__*/React.createElement(FormField, {
      label: "Email",
      defaultValue: c.email,
      prefixIcon: "mail",
      type: "email",
      block: true,
      onChange: dirty
    }), /*#__PURE__*/React.createElement(Autocomplete, {
      label: "Ciudad",
      options: cities,
      defaultValue: c.city,
      prefixIcon: "location_on",
      block: true,
      onChange: dirty
    }), /*#__PURE__*/React.createElement(FormField, {
      label: "Tel\xE9fono",
      defaultValue: "+34 963 000 000",
      prefixIcon: "call",
      hint: "Con prefijo internacional",
      block: true,
      onChange: dirty
    })), /*#__PURE__*/React.createElement(FormField, {
      label: "Notas internas",
      multiline: true,
      hintEnd: "0/500",
      block: true,
      onChange: dirty
    })), /*#__PURE__*/React.createElement(Accordion, null, /*#__PURE__*/React.createElement(ExpansionPanel, {
      title: "Facturaci\xF3n",
      description: "Forma de pago, IBAN y descuentos",
      defaultExpanded: true
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 12
      }
    }, /*#__PURE__*/React.createElement(RadioGroup, {
      defaultValue: "sepa",
      direction: "row",
      ariaLabel: "Forma de pago",
      onChange: dirty
    }, /*#__PURE__*/React.createElement(Radio, {
      value: "sepa"
    }, "Domiciliaci\xF3n SEPA"), /*#__PURE__*/React.createElement(Radio, {
      value: "transfer"
    }, "Transferencia"), /*#__PURE__*/React.createElement(Radio, {
      value: "card"
    }, "Tarjeta")), /*#__PURE__*/React.createElement("div", {
      style: grid
    }, /*#__PURE__*/React.createElement(FormField, {
      label: "IBAN",
      defaultValue: "ES91 2100 0418 4502 0005 1332",
      block: true,
      onChange: dirty
    }), /*#__PURE__*/React.createElement(Select, {
      label: "Vencimiento",
      defaultValue: "30",
      options: [{
        value: '0',
        label: 'Al contado'
      }, {
        value: '30',
        label: '30 días'
      }, {
        value: '60',
        label: '60 días'
      }],
      block: true,
      onChange: dirty
    })), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--mat-sys-body-small)',
        color: 'var(--mat-sys-on-surface-variant)'
      }
    }, "Descuento comercial"), /*#__PURE__*/React.createElement(Slider, {
      min: 0,
      max: 20,
      step: 1,
      defaultValue: 5,
      discrete: true,
      showTickMarks: true,
      displayWith: v => v + ' %',
      width: 320,
      ariaLabel: "Descuento comercial",
      onChange: dirty
    })))), /*#__PURE__*/React.createElement(ExpansionPanel, {
      title: "Comunicaciones",
      description: "Avisos y consentimientos"
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 4
      }
    }, /*#__PURE__*/React.createElement(SlideToggle, {
      defaultChecked: true,
      onChange: dirty
    }, "Enviar facturas por email"), /*#__PURE__*/React.createElement(SlideToggle, {
      onChange: dirty
    }, "Recordatorios de cobro autom\xE1ticos"), /*#__PURE__*/React.createElement(Checkbox, {
      defaultChecked: true,
      onChange: dirty
    }, "Consentimiento RGPD firmado"))))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 16
      }
    }, /*#__PURE__*/React.createElement(Card, {
      appearance: "filled",
      title: "Resumen"
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 12
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--mat-sys-body-small)'
      }
    }, "Facturado"), /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--mat-sys-title-large)',
        color: 'var(--mat-sys-on-surface)'
      }
    }, eur(c.total))), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--mat-sys-body-small)'
      }
    }, "Pendiente"), /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--mat-sys-title-large)',
        color: 'var(--mat-sys-on-surface)'
      }
    }, eur(c.total * 0.08))))), /*#__PURE__*/React.createElement(Card, {
      appearance: "outlined",
      title: "Etiquetas"
    }, /*#__PURE__*/React.createElement(ChipSet, {
      role: "grid"
    }, /*#__PURE__*/React.createElement(Chip, {
      kind: "input",
      label: "Preferente"
    }), /*#__PURE__*/React.createElement(Chip, {
      kind: "input",
      label: "Zona Levante"
    }), /*#__PURE__*/React.createElement(Chip, {
      icon: "add",
      label: "A\xF1adir"
    }))), /*#__PURE__*/React.createElement(Card, {
      appearance: "outlined",
      title: "Documentos",
      contentStyle: {
        padding: 0
      }
    }, /*#__PURE__*/React.createElement(List, null, /*#__PURE__*/React.createElement(ListItem, {
      leadingIcon: "picture_as_pdf",
      headline: "Contrato marco 2026.pdf",
      supportingText: "1,2 MB \xB7 12/01/2026",
      trailingIcon: "download",
      onClick: () => {}
    }), /*#__PURE__*/React.createElement(ListItem, {
      leadingIcon: "picture_as_pdf",
      headline: "Certificado AEAT.pdf",
      supportingText: "340 KB \xB7 03/09/2026",
      trailingIcon: "download",
      onClick: () => {}
    })))));
  }
  function CustomerDetail({
    c,
    go,
    toast,
    askDelete
  }) {
    const [dirty, setDirty] = React.useState(false);
    const markDirty = () => setDirty(true);
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        padding: '16px 24px 0'
      }
    }, /*#__PURE__*/React.createElement(IconButton, {
      icon: "arrow_back",
      ariaLabel: "Volver a clientes",
      onClick: () => go('customers')
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        width: 48,
        height: 48,
        borderRadius: '50%',
        background: 'var(--mat-sys-primary-container)',
        color: 'var(--mat-sys-on-primary-container)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        font: 'var(--mat-sys-title-medium)'
      }
    }, c.name.split(' ').slice(0, 2).map(w => w[0]).join('')), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1,
        minWidth: 0
      }
    }, /*#__PURE__*/React.createElement("h1", {
      style: {
        margin: 0,
        font: 'var(--mat-sys-headline-small)'
      }
    }, c.name), /*#__PURE__*/React.createElement("div", {
      style: {
        color: 'var(--mat-sys-on-surface-variant)',
        display: 'flex',
        alignItems: 'center',
        gap: 6
      }
    }, /*#__PURE__*/React.createElement(Icon, {
      name: c.status === 'Activo' ? 'check_circle' : 'schedule',
      size: 18
    }), c.status, " \xB7 N\xBA ", c.id, " \xB7 ", c.city)), /*#__PURE__*/React.createElement(Menu, {
      position: "below-before",
      trigger: /*#__PURE__*/React.createElement(IconButton, {
        icon: "more_vert",
        ariaLabel: "M\xE1s acciones"
      }),
      items: [{
        icon: 'content_copy',
        label: 'Duplicar'
      }, {
        icon: 'archive',
        label: 'Dar de baja'
      }, {
        divider: true
      }, {
        icon: 'delete',
        label: 'Eliminar',
        danger: true,
        onClick: () => askDelete(c)
      }]
    }), /*#__PURE__*/React.createElement(Button, {
      variant: "outlined",
      disabled: !dirty,
      onClick: () => setDirty(false)
    }, "Descartar"), /*#__PURE__*/React.createElement(Button, {
      variant: "filled",
      icon: "save",
      disabled: !dirty,
      onClick: () => {
        setDirty(false);
        toast('Cambios guardados');
      }
    }, "Guardar")), /*#__PURE__*/React.createElement("div", {
      style: {
        padding: '8px 24px 24px'
      }
    }, /*#__PURE__*/React.createElement(Tabs, {
      tabs: [{
        label: 'Datos',
        content: /*#__PURE__*/React.createElement(General, {
          c: c,
          dirty: markDirty
        })
      }, {
        label: 'Pedidos',
        badge: orders.length,
        content: /*#__PURE__*/React.createElement(Card, {
          appearance: "outlined",
          contentStyle: {
            padding: 0
          }
        }, /*#__PURE__*/React.createElement("div", {
          "data-density": "-2"
        }, /*#__PURE__*/React.createElement(Table, {
          rowKey: "id",
          rows: orders,
          columns: [{
            key: 'id',
            header: 'Pedido',
            sortable: true
          }, {
            key: 'date',
            header: 'Fecha'
          }, {
            key: 'status',
            header: 'Estado'
          }, {
            key: 'total',
            header: 'Importe',
            numeric: true,
            sortable: true,
            render: r => eur(r.total)
          }]
        })))
      }, {
        label: 'Facturas',
        content: /*#__PURE__*/React.createElement("div", {
          style: {
            padding: 48,
            textAlign: 'center',
            color: 'var(--mat-sys-on-surface-variant)'
          }
        }, /*#__PURE__*/React.createElement(Icon, {
          name: "request_quote",
          size: 36
        }), /*#__PURE__*/React.createElement("div", {
          style: {
            font: 'var(--mat-sys-title-medium)',
            color: 'var(--mat-sys-on-surface)',
            marginTop: 8
          }
        }, "Sin facturas este a\xF1o"), /*#__PURE__*/React.createElement("div", {
          style: {
            marginTop: 4
          }
        }, "Las facturas emitidas aparecer\xE1n aqu\xED."))
      }, {
        label: 'Historial',
        disabled: true
      }]
    })));
  }
  window.CustomerDetail = CustomerDetail;
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/back-office/CustomerDetail.jsx", error: String((e && e.message) || e) }); }

// ui_kits/back-office/Customers.jsx
try { (() => {
// Customers list: filter bar, dense mat-table with selection + sort, bulk actions, paginator.
(function () {
  const {
    Table,
    Paginator,
    FormField,
    Select,
    Chip,
    ChipSet,
    Button,
    IconButton,
    Menu,
    Tooltip,
    Icon,
    Divider
  } = window.RASSAngularMaterial_6477fb;
  const {
    eur
  } = window.RASS_FMT;
  const statusIcon = {
    Activo: 'check_circle',
    Pendiente: 'schedule',
    Baja: 'block'
  };
  function Customers({
    go,
    customers,
    askDelete
  }) {
    const [q, setQ] = React.useState('');
    const [seg, setSeg] = React.useState('all');
    const [status, setStatus] = React.useState({
      Activo: true,
      Pendiente: false,
      Baja: false
    });
    const [sel, setSel] = React.useState([]);
    const anyStatus = Object.values(status).some(Boolean);
    const rows = customers.filter(c => (!q || (c.name + c.contact + c.city + c.nif).toLowerCase().includes(q.toLowerCase())) && (seg === 'all' || c.segment === seg) && (!anyStatus || status[c.status]));
    return /*#__PURE__*/React.createElement("div", {
      style: {
        padding: 24,
        display: 'flex',
        flexDirection: 'column',
        gap: 16
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 16
      }
    }, /*#__PURE__*/React.createElement("h1", {
      style: {
        margin: 0,
        font: 'var(--mat-sys-headline-small)',
        flex: 1
      }
    }, "Clientes"), /*#__PURE__*/React.createElement(Button, {
      variant: "outlined",
      icon: "download"
    }, "Exportar"), /*#__PURE__*/React.createElement(Button, {
      variant: "filled",
      icon: "add",
      onClick: () => go('new')
    }, "Nuevo cliente")), /*#__PURE__*/React.createElement("div", {
      style: {
        border: '1px solid var(--mat-sys-outline-variant)',
        borderRadius: 'var(--mat-sys-corner-medium)',
        overflow: 'hidden',
        background: 'var(--mat-sys-surface)'
      },
      "data-density": "-2"
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 16,
        padding: '12px 16px 0',
        flexWrap: 'wrap'
      }
    }, /*#__PURE__*/React.createElement(FormField, {
      label: "Buscar",
      prefixIcon: "search",
      value: q,
      onChange: setQ,
      hideSubscript: true,
      width: 300,
      suffix: q ? /*#__PURE__*/React.createElement(IconButton, {
        icon: "close",
        ariaLabel: "Limpiar b\xFAsqueda",
        onClick: () => setQ('')
      }) : null
    }), /*#__PURE__*/React.createElement(Select, {
      label: "Segmento",
      width: 180,
      value: seg,
      onChange: setSeg,
      options: [{
        value: 'all',
        label: 'Todos'
      }, 'Autónomo', 'Pyme', 'Empresa'],
      style: {
        marginBottom: -20
      }
    }), /*#__PURE__*/React.createElement(ChipSet, {
      ariaLabel: "Estado"
    }, Object.keys(status).map(s => /*#__PURE__*/React.createElement(Chip, {
      key: s,
      kind: "filter",
      label: s,
      selected: status[s],
      onChange: v => setStatus({
        ...status,
        [s]: v
      })
    })))), /*#__PURE__*/React.createElement("div", {
      style: {
        height: 12
      }
    }), sel.length > 0 && /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '0 8px 0 16px',
        height: 48,
        background: 'var(--mat-sys-secondary-container)',
        color: 'var(--mat-sys-on-secondary-container)'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        font: 'var(--mat-sys-title-small)',
        flex: 1
      }
    }, sel.length, " seleccionados"), /*#__PURE__*/React.createElement(Button, {
      icon: "mail"
    }, "Enviar email"), /*#__PURE__*/React.createElement(Button, {
      icon: "label"
    }, "Etiquetar"), /*#__PURE__*/React.createElement(Tooltip, {
      message: "Quitar selecci\xF3n"
    }, /*#__PURE__*/React.createElement(IconButton, {
      icon: "close",
      ariaLabel: "Quitar selecci\xF3n",
      onClick: () => setSel([])
    }))), /*#__PURE__*/React.createElement(Divider, null), /*#__PURE__*/React.createElement(Table, {
      rowKey: "id",
      rows: rows,
      selectable: true,
      selected: sel,
      onSelectionChange: setSel,
      defaultSort: {
        active: 'name',
        direction: 'asc'
      },
      onRowClick: r => go('customer', r.id),
      emptyText: "Ning\xFAn cliente coincide con los filtros",
      columns: [{
        key: 'id',
        header: 'Nº',
        sortable: true,
        width: 72
      }, {
        key: 'name',
        header: 'Cliente',
        sortable: true,
        render: r => /*#__PURE__*/React.createElement("div", {
          style: {
            display: 'flex',
            flexDirection: 'column'
          }
        }, /*#__PURE__*/React.createElement("span", null, r.name), /*#__PURE__*/React.createElement("span", {
          style: {
            color: 'var(--mat-sys-on-surface-variant)',
            font: 'var(--mat-sys-body-small)'
          }
        }, r.contact))
      }, {
        key: 'nif',
        header: 'NIF',
        render: r => /*#__PURE__*/React.createElement("span", {
          className: "rass-mono",
          style: {
            fontSize: 13
          }
        }, r.nif)
      }, {
        key: 'city',
        header: 'Ciudad',
        sortable: true
      }, {
        key: 'segment',
        header: 'Segmento',
        sortable: true
      }, {
        key: 'status',
        header: 'Estado',
        render: r => /*#__PURE__*/React.createElement("span", {
          style: {
            display: 'inline-flex',
            alignItems: 'center',
            gap: 6,
            color: r.status === 'Baja' ? 'var(--mat-sys-on-surface-variant)' : undefined
          }
        }, /*#__PURE__*/React.createElement(Icon, {
          name: statusIcon[r.status],
          size: 18,
          style: {
            color: r.status === 'Activo' ? 'var(--mat-sys-primary)' : 'var(--mat-sys-on-surface-variant)'
          }
        }), r.status)
      }, {
        key: 'total',
        header: 'Facturado',
        numeric: true,
        sortable: true,
        render: r => eur(r.total)
      }],
      rowActions: r => /*#__PURE__*/React.createElement(Menu, {
        position: "below-before",
        trigger: /*#__PURE__*/React.createElement(IconButton, {
          icon: "more_vert",
          ariaLabel: 'Acciones para ' + r.name
        }),
        items: [{
          icon: 'visibility',
          label: 'Ver ficha',
          onClick: () => go('customer', r.id)
        }, {
          icon: 'content_copy',
          label: 'Duplicar'
        }, {
          divider: true
        }, {
          icon: 'delete',
          label: 'Eliminar',
          danger: true,
          onClick: () => askDelete(r)
        }]
      })
    }), /*#__PURE__*/React.createElement(Divider, null), /*#__PURE__*/React.createElement(Paginator, {
      length: 248,
      defaultPageSize: 10,
      itemsPerPageLabel: "Elementos por p\xE1gina:",
      ofLabel: "de"
    })));
  }
  window.Customers = Customers;
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/back-office/Customers.jsx", error: String((e && e.message) || e) }); }

// ui_kits/back-office/Dashboard.jsx
try { (() => {
// Dashboard ("Panel"): KPI cards, recent orders table, activity list.
(function () {
  const {
    Card,
    Button,
    IconButton,
    Table,
    List,
    ListItem,
    ButtonToggleGroup,
    ProgressBar,
    Menu,
    Icon
  } = window.RASSAngularMaterial_6477fb;
  const {
    orders,
    activity
  } = window.RASS_DATA;
  const {
    eur
  } = window.RASS_FMT;
  const statusIcon = {
    'En preparación': 'inventory',
    Enviado: 'local_shipping',
    Entregado: 'check_circle',
    Incidencia: 'error'
  };
  function Kpi({
    label,
    value,
    delta,
    up
  }) {
    return /*#__PURE__*/React.createElement(Card, {
      appearance: "outlined"
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--mat-sys-title-small)',
        color: 'var(--mat-sys-on-surface-variant)'
      }
    }, label), /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--mat-sys-headline-medium)',
        color: 'var(--mat-sys-on-surface)',
        marginTop: 8
      }
    }, value), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 4,
        marginTop: 4,
        color: up ? 'var(--mat-sys-primary)' : 'var(--mat-sys-error)'
      }
    }, /*#__PURE__*/React.createElement(Icon, {
      name: up ? 'trending_up' : 'trending_down',
      size: 18
    }), /*#__PURE__*/React.createElement("span", null, delta), /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--mat-sys-on-surface-variant)'
      }
    }, "vs. mes anterior")));
  }
  function Dashboard({
    go
  }) {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        padding: 24,
        display: 'flex',
        flexDirection: 'column',
        gap: 24,
        maxWidth: 1440
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 16,
        flexWrap: 'wrap'
      }
    }, /*#__PURE__*/React.createElement("h1", {
      style: {
        margin: 0,
        font: 'var(--mat-sys-headline-small)',
        flex: 1
      }
    }, "Buenos d\xEDas, Ana"), /*#__PURE__*/React.createElement(ButtonToggleGroup, {
      defaultValue: "month",
      ariaLabel: "Periodo",
      options: [{
        value: 'week',
        label: 'Semana'
      }, {
        value: 'month',
        label: 'Mes'
      }, {
        value: 'year',
        label: 'Año'
      }]
    }), /*#__PURE__*/React.createElement(Button, {
      variant: "filled",
      icon: "add",
      onClick: () => go('new')
    }, "Nuevo cliente")), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit,minmax(220px,1fr))',
        gap: 16
      }
    }, /*#__PURE__*/React.createElement(Kpi, {
      label: "Facturaci\xF3n",
      value: "84.320 \u20AC",
      delta: "+12,4 %",
      up: true
    }), /*#__PURE__*/React.createElement(Kpi, {
      label: "Pedidos",
      value: "312",
      delta: "+4,1 %",
      up: true
    }), /*#__PURE__*/React.createElement(Kpi, {
      label: "Clientes activos",
      value: "1.248",
      delta: "+18",
      up: true
    }), /*#__PURE__*/React.createElement(Kpi, {
      label: "Cobros pendientes",
      value: "9.870 \u20AC",
      delta: "+2,3 %"
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0,2fr) minmax(280px,1fr)',
        gap: 16,
        alignItems: 'start'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      appearance: "outlined",
      title: "\xDAltimos pedidos",
      headerAction: /*#__PURE__*/React.createElement(Button, {
        trailingIcon: "arrow_forward"
      }, "Ver todos"),
      contentStyle: {
        padding: '8px 0 0'
      }
    }, /*#__PURE__*/React.createElement("div", {
      "data-density": "-2"
    }, /*#__PURE__*/React.createElement(Table, {
      rowKey: "id",
      rows: orders,
      onRowClick: () => {},
      columns: [{
        key: 'id',
        header: 'Pedido',
        render: r => /*#__PURE__*/React.createElement("span", {
          className: "rass-mono",
          style: {
            fontSize: 13
          }
        }, r.id)
      }, {
        key: 'customer',
        header: 'Cliente'
      }, {
        key: 'date',
        header: 'Fecha'
      }, {
        key: 'status',
        header: 'Estado',
        render: r => /*#__PURE__*/React.createElement("span", {
          style: {
            display: 'inline-flex',
            alignItems: 'center',
            gap: 6,
            color: r.status === 'Incidencia' ? 'var(--mat-sys-error)' : undefined
          }
        }, /*#__PURE__*/React.createElement(Icon, {
          name: statusIcon[r.status],
          size: 18
        }), r.status)
      }, {
        key: 'total',
        header: 'Importe',
        numeric: true,
        render: r => eur(r.total)
      }]
    }))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 16
      }
    }, /*#__PURE__*/React.createElement(Card, {
      appearance: "outlined",
      title: "Actividad",
      contentStyle: {
        padding: 0
      }
    }, /*#__PURE__*/React.createElement(List, null, activity.map(a => /*#__PURE__*/React.createElement(ListItem, {
      key: a.text,
      avatar: a.who,
      headline: a.name,
      supportingText: a.text,
      trailingText: a.when
    })))), /*#__PURE__*/React.createElement(Card, {
      appearance: "filled",
      title: "Objetivo trimestral",
      subtitle: "Q4 \xB7 250.000 \u20AC"
    }, /*#__PURE__*/React.createElement(ProgressBar, {
      value: 34,
      ariaLabel: "Progreso objetivo"
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 8
      }
    }, "34 % alcanzado \xB7 quedan 84 d\xEDas")))));
  }
  window.Dashboard = Dashboard;
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/back-office/Dashboard.jsx", error: String((e && e.message) || e) }); }

// ui_kits/back-office/NewCustomer.jsx
try { (() => {
// New customer wizard: mat-stepper (linear) inside an outlined card.
(function () {
  const {
    Stepper,
    Card,
    FormField,
    Select,
    Checkbox,
    Button,
    Icon,
    ProgressBar
  } = window.RASSAngularMaterial_6477fb;
  function NewCustomer({
    go,
    toast,
    onCreate
  }) {
    const [step, setStep] = React.useState(0);
    const [f, setF] = React.useState({
      name: '',
      nif: '',
      segment: '',
      contact: '',
      email: '',
      city: '',
      rgpd: false
    });
    const [touched, setTouched] = React.useState({});
    const [saving, setSaving] = React.useState(false);
    const set = k => v => setF({
      ...f,
      [k]: v
    });
    const err = {
      name: !f.name && 'La razón social es obligatoria',
      nif: !f.nif && 'El NIF es obligatorio' || !/^[A-Z0-9]{9}$/i.test(f.nif) && 'El NIF debe tener 9 caracteres',
      email: f.email && !/^\S+@\S+\.\S+$/.test(f.email) && 'Introduce un email válido'
    };
    const ok0 = !err.name && !err.nif && f.segment;
    const ok1 = f.contact && f.email && !err.email;
    const show = k => touched[k] && err[k];
    const touch = ks => setTouched({
      ...touched,
      ...Object.fromEntries(ks.map(k => [k, true]))
    });
    const grid = {
      display: 'grid',
      gridTemplateColumns: 'repeat(auto-fill,minmax(260px,1fr))',
      columnGap: 16,
      rowGap: 4
    };
    const actions = (back, next, onNext) => /*#__PURE__*/React.createElement("div", {
      className: "rass-stepper__actions"
    }, back && /*#__PURE__*/React.createElement(Button, {
      onClick: () => setStep(step - 1)
    }, "Atr\xE1s"), /*#__PURE__*/React.createElement(Button, {
      variant: "filled",
      onClick: onNext
    }, next));
    const submit = () => {
      setSaving(true);
      setTimeout(() => {
        setSaving(false);
        onCreate(f);
        toast('Cliente «' + f.name + '» creado');
      }, 900);
    };
    return /*#__PURE__*/React.createElement("div", {
      style: {
        padding: 24,
        maxWidth: 960
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 16,
        marginBottom: 16
      }
    }, /*#__PURE__*/React.createElement("h1", {
      style: {
        margin: 0,
        font: 'var(--mat-sys-headline-small)',
        flex: 1
      }
    }, "Nuevo cliente"), /*#__PURE__*/React.createElement(Button, {
      onClick: () => go('customers')
    }, "Cancelar")), /*#__PURE__*/React.createElement(Card, {
      appearance: "outlined",
      contentStyle: {
        padding: 0
      }
    }, saving && /*#__PURE__*/React.createElement(ProgressBar, {
      mode: "indeterminate",
      ariaLabel: "Guardando"
    }), /*#__PURE__*/React.createElement(Stepper, {
      linear: true,
      selectedIndex: step,
      onChange: setStep,
      steps: [{
        label: 'Datos fiscales',
        completed: !!ok0,
        content: /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
          style: grid
        }, /*#__PURE__*/React.createElement(FormField, {
          label: "Raz\xF3n social",
          value: f.name,
          onChange: set('name'),
          onBlur: () => touch(['name']),
          required: true,
          error: show('name'),
          block: true
        }), /*#__PURE__*/React.createElement(FormField, {
          label: "NIF / CIF",
          value: f.nif,
          onChange: v => set('nif')(v.toUpperCase()),
          onBlur: () => touch(['nif']),
          required: true,
          error: show('nif'),
          hint: "9 caracteres, sin guiones",
          block: true
        }), /*#__PURE__*/React.createElement(Select, {
          label: "Segmento",
          value: f.segment,
          onChange: set('segment'),
          required: true,
          options: ['Autónomo', 'Pyme', 'Empresa'],
          block: true
        })), actions(false, 'Siguiente', () => {
          touch(['name', 'nif']);
          if (ok0) setStep(1);
        }))
      }, {
        label: 'Contacto',
        completed: !!ok1,
        content: /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
          style: grid
        }, /*#__PURE__*/React.createElement(FormField, {
          label: "Persona de contacto",
          value: f.contact,
          onChange: set('contact'),
          required: true,
          prefixIcon: "person",
          block: true
        }), /*#__PURE__*/React.createElement(FormField, {
          label: "Email",
          value: f.email,
          onChange: set('email'),
          onBlur: () => touch(['email']),
          required: true,
          type: "email",
          prefixIcon: "mail",
          error: show('email'),
          block: true
        }), /*#__PURE__*/React.createElement(FormField, {
          label: "Ciudad",
          value: f.city,
          onChange: set('city'),
          prefixIcon: "location_on",
          hint: "Opcional",
          block: true
        })), actions(true, 'Siguiente', () => {
          touch(['email']);
          if (ok1) setStep(2);
        }))
      }, {
        label: 'Confirmar',
        content: /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
          style: {
            display: 'grid',
            gridTemplateColumns: '160px 1fr',
            rowGap: 8,
            color: 'var(--mat-sys-on-surface)'
          }
        }, [['Razón social', f.name], ['NIF', f.nif], ['Segmento', f.segment], ['Contacto', f.contact], ['Email', f.email], ['Ciudad', f.city || '—']].map(([k, v]) => /*#__PURE__*/React.createElement(React.Fragment, {
          key: k
        }, /*#__PURE__*/React.createElement("span", {
          style: {
            color: 'var(--mat-sys-on-surface-variant)'
          }
        }, k), /*#__PURE__*/React.createElement("span", null, v)))), /*#__PURE__*/React.createElement("div", {
          style: {
            marginTop: 12
          }
        }, /*#__PURE__*/React.createElement(Checkbox, {
          checked: f.rgpd,
          onChange: set('rgpd')
        }, "El cliente ha firmado el consentimiento RGPD")), /*#__PURE__*/React.createElement("div", {
          className: "rass-stepper__actions"
        }, /*#__PURE__*/React.createElement(Button, {
          onClick: () => setStep(1)
        }, "Atr\xE1s"), /*#__PURE__*/React.createElement(Button, {
          variant: "filled",
          icon: "check",
          disabled: !f.rgpd || saving,
          onClick: submit
        }, "Crear cliente")))
      }]
    })));
  }
  window.NewCustomer = NewCustomer;
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/back-office/NewCustomer.jsx", error: String((e && e.message) || e) }); }

// ui_kits/back-office/data.js
try { (() => {
// Sample data for the RASS back-office reference kit (fictional).
window.RASS_DATA = {
  customers: [{
    id: 1042,
    name: 'Ferretería Ruiz S.L.',
    contact: 'María Ruiz',
    email: 'maria.ruiz@ferreteriaruiz.es',
    city: 'Valencia',
    segment: 'Pyme',
    status: 'Activo',
    total: 12450.5,
    since: '2019-03-14',
    nif: 'B46123456'
  }, {
    id: 1043,
    name: 'Clínica Dental Mar',
    contact: 'Carmen Pérez',
    email: 'cperez@dentalmar.es',
    city: 'Alicante',
    segment: 'Pyme',
    status: 'Pendiente',
    total: 3200,
    since: '2024-11-02',
    nif: 'B03987654'
  }, {
    id: 1044,
    name: 'Transportes Ibéricos S.A.',
    contact: 'Jorge López',
    email: 'jlopez@tibericos.com',
    city: 'Madrid',
    segment: 'Empresa',
    status: 'Activo',
    total: 48910.75,
    since: '2017-06-21',
    nif: 'A28456789'
  }, {
    id: 1045,
    name: 'Panadería La Espiga',
    contact: 'Luis Martín',
    email: 'hola@laespiga.es',
    city: 'Sevilla',
    segment: 'Autónomo',
    status: 'Baja',
    total: 860,
    since: '2022-01-10',
    nif: '28765432K'
  }, {
    id: 1046,
    name: 'Grupo Norte Asesores',
    contact: 'Ane Etxeberria',
    email: 'ane@gruponorte.eus',
    city: 'Bilbao',
    segment: 'Empresa',
    status: 'Activo',
    total: 22015,
    since: '2020-09-30',
    nif: 'B95112233'
  }, {
    id: 1047,
    name: 'Viveros El Olivo',
    contact: 'Rosa Jiménez',
    email: 'rosa@viveroselolivo.es',
    city: 'Córdoba',
    segment: 'Pyme',
    status: 'Activo',
    total: 7340.2,
    since: '2021-04-18',
    nif: 'B14556677'
  }, {
    id: 1048,
    name: 'Talleres Garrido',
    contact: 'Pedro Garrido',
    email: 'taller@garrido.es',
    city: 'Zaragoza',
    segment: 'Autónomo',
    status: 'Pendiente',
    total: 1980,
    since: '2025-02-07',
    nif: '17654321P'
  }, {
    id: 1049,
    name: 'Óptica Visión Clara',
    contact: 'Laura Sanz',
    email: 'laura@visionclara.es',
    city: 'Valencia',
    segment: 'Pyme',
    status: 'Activo',
    total: 15620,
    since: '2018-12-03',
    nif: 'B46998877'
  }, {
    id: 1050,
    name: 'Construcciones Levante',
    contact: 'Andrés Vidal',
    email: 'avidal@clevante.es',
    city: 'Castellón',
    segment: 'Empresa',
    status: 'Activo',
    total: 96300,
    since: '2016-05-25',
    nif: 'A12334455'
  }, {
    id: 1051,
    name: 'Librería Papiro',
    contact: 'Elena Gil',
    email: 'elena@papiro.es',
    city: 'Salamanca',
    segment: 'Autónomo',
    status: 'Activo',
    total: 2410.9,
    since: '2023-08-14',
    nif: '07123987L'
  }],
  orders: [{
    id: 'PED-26-0981',
    customer: 'Construcciones Levante',
    date: '07/10/2026',
    status: 'En preparación',
    total: 8420
  }, {
    id: 'PED-26-0980',
    customer: 'Ferretería Ruiz S.L.',
    date: '07/10/2026',
    status: 'Enviado',
    total: 1265.4
  }, {
    id: 'PED-26-0979',
    customer: 'Óptica Visión Clara',
    date: '06/10/2026',
    status: 'Entregado',
    total: 640
  }, {
    id: 'PED-26-0978',
    customer: 'Transportes Ibéricos S.A.',
    date: '06/10/2026',
    status: 'Incidencia',
    total: 3990
  }, {
    id: 'PED-26-0977',
    customer: 'Grupo Norte Asesores',
    date: '05/10/2026',
    status: 'Entregado',
    total: 1180
  }],
  activity: [{
    who: 'MR',
    name: 'María Ruiz',
    text: 'Aprobó el presupuesto PRE-26-0412',
    when: '10:42'
  }, {
    who: 'JL',
    name: 'Jorge López',
    text: 'Abrió una incidencia en PED-26-0978',
    when: '09:15'
  }, {
    who: 'AV',
    name: 'Andrés Vidal',
    text: 'Subió 3 documentos',
    when: 'Ayer'
  }]
};
window.RASS_FMT = {
  eur: n => n.toLocaleString('es-ES', {
    style: 'currency',
    currency: 'EUR'
  }),
  date: s => new Date(s).toLocaleDateString('es-ES', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  })
};
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/back-office/data.js", error: String((e && e.message) || e) }); }

__ds_ns.Badge = __ds_scope.Badge;

__ds_ns.Button = __ds_scope.Button;

__ds_ns.ButtonToggleGroup = __ds_scope.ButtonToggleGroup;

__ds_ns.Fab = __ds_scope.Fab;

__ds_ns.IconButton = __ds_scope.IconButton;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.Chip = __ds_scope.Chip;

__ds_ns.ChipSet = __ds_scope.ChipSet;

__ds_ns.Accordion = __ds_scope.Accordion;

__ds_ns.ExpansionPanel = __ds_scope.ExpansionPanel;

__ds_ns.Autocomplete = __ds_scope.Autocomplete;

__ds_ns.Calendar = __ds_scope.Calendar;

__ds_ns.Datepicker = __ds_scope.Datepicker;

__ds_ns.FormField = __ds_scope.FormField;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.Divider = __ds_scope.Divider;

__ds_ns.Icon = __ds_scope.Icon;

__ds_ns.List = __ds_scope.List;

__ds_ns.ListItem = __ds_scope.ListItem;

__ds_ns.Menu = __ds_scope.Menu;

__ds_ns.BottomSheet = __ds_scope.BottomSheet;

__ds_ns.Dialog = __ds_scope.Dialog;

__ds_ns.Snackbar = __ds_scope.Snackbar;

__ds_ns.Tooltip = __ds_scope.Tooltip;

__ds_ns.ProgressBar = __ds_scope.ProgressBar;

__ds_ns.ProgressSpinner = __ds_scope.ProgressSpinner;

__ds_ns.Checkbox = __ds_scope.Checkbox;

__ds_ns.RadioGroup = __ds_scope.RadioGroup;

__ds_ns.Radio = __ds_scope.Radio;

__ds_ns.SlideToggle = __ds_scope.SlideToggle;

__ds_ns.Sidenav = __ds_scope.Sidenav;

__ds_ns.NavSection = __ds_scope.NavSection;

__ds_ns.NavItem = __ds_scope.NavItem;

__ds_ns.Slider = __ds_scope.Slider;

__ds_ns.Stepper = __ds_scope.Stepper;

__ds_ns.Paginator = __ds_scope.Paginator;

__ds_ns.Table = __ds_scope.Table;

__ds_ns.Tabs = __ds_scope.Tabs;

__ds_ns.Toolbar = __ds_scope.Toolbar;

})();
