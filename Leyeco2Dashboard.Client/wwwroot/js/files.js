// The console's one way to hand the user a file. A printout is fetched through the authenticated HttpClient (the
// session lives in HttpOnly cookies a plain link would carry too, but its failures would then land as a page), so it
// arrives as bytes in .NET; this gives them to the browser as a named download (an anchor on an object URL).
// The HRIS's appFileIo.saveAs, under this console's name.
window.monitorFiles = {
    saveAs: function (fileName, base64, mime) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }

        const blob = new Blob([bytes], mime ? { type: mime } : undefined);
        const url = URL.createObjectURL(blob);

        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName || 'download';
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);

        // Give the click a beat to start the download before the URL goes.
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }
};
