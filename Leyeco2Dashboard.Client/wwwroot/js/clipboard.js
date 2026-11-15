// The Copy buttons' one call: put text on the clipboard and answer whether it got there.
// navigator.clipboard needs a secure context (https, or localhost). The console may be served over plain http
// inside the cooperative's network, so a hidden textarea and execCommand('copy') stand in there. The textarea
// goes inside the open dialog, if there is one, so focus never leaves it.
window.monitorClipboard = {
    copy: async function (text) {
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch {
                // Refused (no permission, document not focused): try the old way.
            }
        }

        const active = document.activeElement;
        const area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.setAttribute('aria-hidden', 'true');
        area.style.cssText = 'position:fixed;top:0;left:0;width:1px;height:1px;opacity:0;pointer-events:none;';
        (active?.closest('[role="dialog"]') ?? document.body).appendChild(area);
        area.select();

        let copied = false;
        try {
            copied = document.execCommand('copy');
        } catch {
            copied = false;
        }

        area.remove();
        if (active instanceof HTMLElement)
            active.focus({ preventScroll: true });

        return copied;
    }
};
