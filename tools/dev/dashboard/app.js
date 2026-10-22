'use strict';
const element = id => document.getElementById(id);
const readable = value => value ? new Date(value).toLocaleString() : 'Not recorded';
function render(data) {
  const check = data.lastCheck || {};
  const state = ['passed', 'failed', 'running'].includes(check.state) ? check.state : 'unknown';
  element('check-state').textContent = state[0].toUpperCase() + state.slice(1);
  element('check-state').className = state;
  element('check-detail').textContent = check.reason || (check.exitCode == null ? 'No completed check recorded.' : `Process exit code ${check.exitCode}`);
  element('check-time').textContent = `Finished: ${readable(check.finishedAtUtc)}`;
  element('repository').textContent = data.repositoryRoot || 'Unknown';
  element('observed').textContent = `OBSERVED ${readable(data.observedAtUtc)}`;
  const steps = element('steps');
  steps.replaceChildren();
  for (const name of ['restore', 'build', 'test']) {
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
    const response = await fetch('/status.json', {cache: 'no-store'});
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    render(await response.json());
    element('connection').textContent = '● Connected to your local observer';
  } catch (error) {
    element('connection').textContent = `Status unavailable: ${error.message}. Previously displayed results may be stale.`;
    element('check-state').textContent = 'Unavailable';
    element('check-state').className = '';
  }
}
refresh();
setInterval(refresh, 10000);
