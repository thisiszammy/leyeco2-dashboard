// The shell's one question of the window: is it too narrow for the full sidebar beside a page? MainLayout asks
// once, when no sidebar choice is remembered, and opens on the rail if so. 768px matches app.css's breakpoint
// where the panel grids fold to one column.
window.monitorLayout = {
    isNarrow: () => window.matchMedia('(max-width: 768px)').matches
};
