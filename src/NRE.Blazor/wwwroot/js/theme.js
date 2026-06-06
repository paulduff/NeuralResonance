window.Theme = {
  setDark: function(isDark){
    if(isDark) document.body.classList.add("dark");
    else document.body.classList.remove("dark");
  }
};

window.NreSaveLoad = {
  downloadBlob: function(base64, fileName) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }
};
