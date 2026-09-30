(() => {
  const DB_NAME = "FarmFlowOffline";
  const STORE = "queue";
  const dbOpen = () => new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE, { keyPath: "clientId" });
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
  const all = async () => { const db = await dbOpen(); return new Promise((resolve,reject) => { const r=db.transaction(STORE).objectStore(STORE).getAll(); r.onsuccess=()=>resolve(r.result); r.onerror=()=>reject(r.error); }); };
  const put = async item => { const db=await dbOpen(); return new Promise((resolve,reject)=>{const r=db.transaction(STORE,"readwrite").objectStore(STORE).put(item);r.onsuccess=()=>resolve();r.onerror=()=>reject(r.error);}); };
  const remove = async id => { const db=await dbOpen(); return new Promise((resolve,reject)=>{const r=db.transaction(STORE,"readwrite").objectStore(STORE).delete(id);r.onsuccess=()=>resolve();r.onerror=()=>reject(r.error);}); };
  const makeId = () => crypto.randomUUID ? crypto.randomUUID() : String(Date.now()) + "-" + String(Math.random());
  const currentUser = () => window.farmFlowUserId || localStorage.getItem("farmflow.user");
  const status = message => { const el=document.getElementById("offlineQueueStatus"); if(el) el.textContent=message; const badge=document.getElementById("offlineStatus"); if(badge) badge.textContent=navigator.onLine ? "Online" : "Offline"; };
  async function sync() {
    if (!navigator.onLine) { status("Offline — entries are stored on this phone."); return; }
    const items = (await all()).filter(x => x.ownerKey === currentUser());
    if (!items.length) { status("Online — everything is synced."); return; }
    try {
      const response = await fetch("/Offline/Sync", { method:"POST", credentials:"same-origin", headers:{"Content-Type":"application/json"}, body:JSON.stringify({items}) });
      if (response.status === 401) { status(String(items.length) + " item(s) waiting — sign in to sync."); return; }
      const data = await response.json();
      for (const result of data.results || []) if (result.ok) await remove(result.clientId);
      const remaining = await all();
      status(remaining.length ? String(remaining.length) + " item(s) still waiting to sync." : "Online — everything is synced.");
    } catch { status(String(items.length) + " item(s) waiting — will sync when signal returns."); }
  }
  async function queue(type, data) {
    const ownerKey = currentUser();
    if (!ownerKey) { status("Sign in while online before recording offline entries."); return; }
    await put({ clientId:makeId(), ownerKey, type, data, queuedAt:new Date().toISOString() });
    status("Saved on this phone. It will sync automatically when online.");
    if (navigator.onLine) sync();
  }
  window.FarmFlowOffline = { sync, queue, pending: all };
  window.addEventListener("online", sync);
  window.addEventListener("offline", () => status("Offline — entries are stored on this phone."));
  document.addEventListener("DOMContentLoaded", () => { status(navigator.onLine ? "Online — everything is synced." : "Offline — entries are stored on this phone."); sync(); });
})();
