// Photo ownership rules shared by the page and dependency-free Node tests.
// A photo keeps loading and resizing after the user switches position, so each widget
// writes to the draft that started it and only the widgets on screen can change the
// busy state. Without this, a slow photo lands on whichever position is selected when
// it finishes.
export function createEvidenceBinding({ createWidget, draftFor, onValueChange, onBusyChange }) {
  let epoch = 0;
  let live = [];
  let busy = 0;

  function mount(ownerId, widgets) {
    for (const widget of live) widget.cancel?.();
    live = [];
    epoch += 1;
    busy = 0;
    onBusyChange(busy);
    const owner = draftFor(ownerId);
    const mine = epoch;
    for (const { root, field, label, hint } of widgets) {
      if (!root) continue;
      const widget = createWidget(root, {
        label,
        hint,
        onChange(value) {
          owner[field] = value;
          if (mine === epoch) onValueChange(field, value);
        },
        onBusyChange(isBusy) {
          if (mine !== epoch) return;
          busy = Math.max(0, busy + (isBusy ? 1 : -1));
          onBusyChange(busy);
        },
      });
      widget.setValue(owner[field] || '');
      live.push(widget);
    }
  }

  return { mount, busyCount: () => busy };
}
