const kind = arguments[0];
const norm = s => (s || '').replace(/\s+/g, ' ').trim();
const txt = n => norm(n && n.textContent);
const ctlSel = 'input,select,textarea';
const isVis = e => {
  if (!e.getClientRects().length) return false;
  const cs = getComputedStyle(e);
  return cs.visibility !== 'hidden' && cs.display !== 'none';
};
const hasCtl = n => n.nodeType === 1 && (n.matches(ctlSel) || !!n.querySelector(ctlSel));
const labelText = l => {
  const c = l.cloneNode(true);
  c.querySelectorAll(ctlSel).forEach(x => x.remove());
  return txt(c);
};

// Closest text that sits before `start` (climbs up to 4 ancestors); stops at other controls.
function nearby(start) {
  let node = start;
  for (let d = 0; node && node !== document.body && d < 4; d++, node = node.parentElement) {
    let p = node.previousSibling;
    while (p) {
      if (p.nodeType === 3) { const t = norm(p.textContent); if (t) return t; }
      else if (p.nodeType === 1) { if (hasCtl(p)) break; const t = txt(p); if (t) return t; }
      p = p.previousSibling;
    }
  }
  return '';
}
// Text right after a control, e.g. <input type=radio> Male
function after(e) {
  let n = e.nextSibling;
  while (n) {
    if (n.nodeType === 3) { const t = norm(n.textContent); if (t) return t; }
    else if (n.nodeType === 1) { if (hasCtl(n)) return ''; const t = txt(n); if (t) return t; }
    n = n.nextSibling;
  }
  return '';
}
// Text at the start of a container, before its first control
function leading(c) {
  let out = '';
  for (const ch of c.childNodes) {
    if (ch.nodeType === 3) out += ' ' + ch.textContent;
    else if (ch.nodeType === 1) { if (hasCtl(ch)) break; out += ' ' + ch.textContent; }
  }
  return norm(out);
}
function aria(e, out) {
  const al = e.getAttribute('aria-label');
  if (al) out.push({ src: 'aria-label', text: norm(al) });
  const lb = e.getAttribute('aria-labelledby');
  if (lb) lb.split(/\s+/).forEach(id => { const n = document.getElementById(id); if (n) out.push({ src: 'aria-labelledby', text: txt(n) }); });
}
function own(e) {
  const out = [];
  if (e.id) document.querySelectorAll('label[for="' + CSS.escape(e.id) + '"]').forEach(l => out.push({ src: 'label-for', text: labelText(l) }));
  const wl = e.closest('label');
  if (wl) out.push({ src: 'label-wrap', text: labelText(wl) });
  aria(e, out);
  if (e.getAttribute('placeholder')) out.push({ src: 'placeholder', text: norm(e.getAttribute('placeholder')) });
  if (e.getAttribute('title')) out.push({ src: 'title', text: norm(e.getAttribute('title')) });
  out.push({ src: 'nearby', text: nearby(wl || e) });
  out.push({ src: 'name-id', text: (e.getAttribute('name') || '') + ' ' + (e.id || '') });
  return out.filter(x => x.text);
}
// For radios: the labels of the whole group ("Gender"), not of one option
function group(e) {
  const same = e.name ? Array.from(document.querySelectorAll('input[type=radio]')).filter(r => r.name === e.name) : [];
  let c = e.parentElement;
  while (c && c !== document.body) {
    const okc = same.length > 1 ? same.every(r => c.contains(r)) : c.querySelectorAll('input[type=radio]').length > 1;
    if (okc) break;
    c = c.parentElement;
  }
  if (!c || c === document.body) c = e.parentElement || document.body;
  const out = [];
  const fs = c.closest('fieldset');
  if (fs) { const lg = fs.querySelector('legend'); if (lg) out.push({ src: 'legend', text: txt(lg) }); }
  aria(c, out);
  out.push({ src: 'nearby', text: nearby(c) });
  out.push({ src: 'leading', text: leading(c) });
  if (e.name) out.push({ src: 'name-id', text: e.name });
  out.push({ src: 'name-id', text: (c.id || '') + ' ' + (c.getAttribute('name') || '') });
  return out.filter(x => x.text.trim());
}

let els;
if (kind === 'select') els = Array.from(document.querySelectorAll('select')).filter(e => !e.disabled && isVis(e));
else if (kind === 'radio') els = Array.from(document.querySelectorAll('input[type=radio]')).filter(e => !e.disabled);
else els = Array.from(document.querySelectorAll('input')).filter(e => {
  const t = (e.getAttribute('type') || 'text').toLowerCase();
  return ['text', 'search', ''].includes(t) && !e.disabled && !e.readOnly && isVis(e);
});

let k = window.__rqc || 0;
const res = els.map(e => {
  k++;
  e.setAttribute('data-rq-id', String(k));   // private handle, so C# can re-find the element
  const o = { id: k, visible: isVis(e), checked: !!e.checked, labels: own(e), group: [], options: [] };
  if (kind === 'radio') {
    const a = after(e); if (a) o.labels.push({ src: 'after', text: a });
    // a neighbouring option's text must not become this option's label when a real label exists
    if (o.labels.some(l => ['label-for', 'label-wrap', 'aria-label', 'aria-labelledby', 'after'].includes(l.src)))
      o.labels = o.labels.filter(l => l.src !== 'nearby');
    if (e.value) o.labels.push({ src: 'value', text: e.value });
    o.group = group(e);
  }
  if (kind === 'select') o.options = Array.from(e.options).map(x => norm(x.text));
  return o;
});
window.__rqc = k;
return JSON.stringify(res);
