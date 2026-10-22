'use strict';
const element = id => document.getElementById(id);
const readable = value => value ? new Date(value).toLocaleString() : 'Not recorded';
function receiptState(id, receipt) {
  const state = ['passed', 'failed', 'stale'].includes(receipt.state) ? receipt.state : 'unknown';
  element(id).textContent = state === 'stale' ? `Stale · last ${['passed', 'failed'].includes(receipt.outcome) ? receipt.outcome : 'unknown'}` : state[0].toUpperCase() + state.slice(1);
  element(id).className = state;
}
function renderUpdater(updater) {
  const health = updater.health || {};
  const healthState = ['online', 'offline'].includes(health.state) ? health.state : 'unknown';
  element('updater-health-state').textContent = healthState[0].toUpperCase() + healthState.slice(1);
  element('updater-health-state').className = healthState;
  element('updater-health-detail').textContent = health.reason || 'HTTP health was not observed.';
  const check = updater.lastCheck || {};
  const smoke = updater.lastSmokeTest || {};
  receiptState('updater-check-state', check);
  receiptState('updater-smoke-state', smoke);
  element('updater-check-time').textContent = readable(check.checkedAtUtc);
  element('updater-smoke-time').textContent = readable(smoke.finishedAtUtc);
  element('updater-release').textContent = check.productId && check.channel ? `${check.productId} · ${check.channel} · release ${check.sequence ?? 'unknown'}` : 'No valid release check recorded.';
  const valid = value => value === true ? 'valid' : value === false ? 'not confirmed' : 'unknown';
  element('updater-check-detail').textContent = `Index: ${valid(check.indexValid)} · Manifest: ${valid(check.manifestValid)} · Bundle/launcher endpoints: ${check.downloadsReachable === true ? 'reachable' : check.downloadsReachable === false ? 'not confirmed' : 'unknown'}. ${check.reason || ''}`;
  element('updater-smoke-detail').textContent = smoke.outcome ? `${smoke.passed} passed · ${smoke.failed} failed · ${smoke.skipped} skipped. ${smoke.reason || ''}` : smoke.reason || 'No updater smoke-test receipt.';
}
function render(data) {
  const check = data.lastCheck || {};
  const state = ['passed', 'failed', 'running'].includes(check.state) ? check.state : 'unknown';
  element('check-state').textContent = state[0].toUpperCase() + state.slice(1);
  element('check-state').className = state;
  element('check-detail').textContent = check.reason || (check.exitCode == null ? 'No completed check recorded.' : `Process exit code ${check.exitCode}`);
  element('check-time').textContent = `Finished: ${readable(check.finishedAtUtc)}`;
  element('repository').textContent = data.repositoryRoot || 'Unknown';
  element('observed').textContent = `OBSERVED ${readable(data.observedAtUtc)}`;
  renderUpdater(data.updater || {});
  const steps = element('steps');
  steps.replaceChildren();
  for (const name of ['restore', 'build', 'test', 'launcher-restore', 'launcher-build', 'launcher-test', 'updater-status-test', 'updater-smoke-parser-test']) {
    const step = (check.steps || []).find(row => row.name === name);
    const card = document.createElement('div');
    card.className = 'step';
    const title = document.createElement('strong');
    title.textContent = name;
    const detail = document.createElement('span');
    detail.textContent = step ? `${step.outcome} · exit ${step.exitCode ?? 'pending'}` : 'Not run';
    card.append(title, detail);
    steps.append(card);
  }
  const services = data.services || {};
  const items = services.items || [];
  element('service-count').textContent = services.state === 'available' ? String(items.length) : 'Unknown';
  element('service-detail').textContent = services.reason || 'Processes attributed to the explicitly selected installation.';
  const list = element('services');
  list.replaceChildren();
  if (!items.length) {
    const note = document.createElement('p');
    note.className = 'muted';
    note.textContent = services.reason || 'No processes attributed to this installation. Other installations are not inferred from open ports.';
    list.append(note);
  }
  for (const item of items) {
    const row = document.createElement('div');
    row.className = 'service';
    const title = document.createElement('strong');
    title.textContent = `${item.service} · PID ${item.processId}`;
    const detail = document.createElement('span');
    detail.textContent = (item.listeners || []).map(listener => `${listener.address}:${listener.port}`).join(' · ') || 'No listening port observed';
    row.append(title, detail);
    list.append(row);
  }
}
async function refresh() {
  try {
    const response = await fetch('/api/status', {cache: 'no-store'});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    render(await response.json());
    element('connection').textContent = '● Connected to your local observer';
  } catch (error) {
    element('connection').textContent = `Status unavailable: ${error.message}. Previously displayed results may be stale.`;
    element('check-state').textContent = 'Unavailable';
    element('check-state').className = '';
    element('updater-health-state').textContent = 'Unavailable';
    element('updater-health-state').className = 'unknown';
    for (const id of ['updater-check-state', 'updater-smoke-state']) {
      element(id).textContent = 'Unavailable · previous result may be stale';
      element(id).className = 'unknown';
    }
  }
}
refresh();
setInterval(refresh, 10000);
