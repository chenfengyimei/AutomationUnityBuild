const state = {
  user: null,
  projects: [],
  configs: [],
  jobs: [],
  selectedJobId: null,
  activeTab: "builds",
};

const $ = (id) => document.getElementById(id);

async function api(path, options = {}) {
  const response = await fetch(path, {
    credentials: "include",
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options,
  });
  if (response.status === 401) throw new Error("未登录或登录已过期");
  if (!response.ok) throw new Error(await readErrorMessage(response));
  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

async function readErrorMessage(response) {
  const text = await response.text();
  if (!text) return response.statusText || `HTTP ${response.status}`;

  try {
    const data = JSON.parse(text);
    return data.error || data.message || text;
  } catch {
    return text;
  }
}

function showError(error) {
  const message = error instanceof Error ? error.message : String(error || "");
  if (message) alert(message);
}

async function init() {
  bindEvents();
  window.addEventListener("unhandledrejection", (event) => {
    event.preventDefault();
    showError(event.reason);
  });
  try {
    state.user = await api("/api/me");
    showMain();
    await refreshAll();
  } catch {
    showLogin();
  }
  setInterval(refreshJobsSoft, 3000);
}

function bindEvents() {
  $("loginForm").addEventListener("submit", login);
  $("logoutBtn").addEventListener("click", logout);
  $("refreshBtn").addEventListener("click", refreshAll);
  $("projectForm").addEventListener("submit", createProject);
  $("configForm").addEventListener("submit", createConfig);
  $("buildForm").addEventListener("submit", startBuild);
  document.querySelectorAll("aside button[data-tab]").forEach((button) => {
    button.addEventListener("click", () => setTab(button.dataset.tab));
  });
  $("buildProject").addEventListener("change", renderBuildConfigs);
}

async function login(event) {
  event.preventDefault();
  state.user = await api("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ userName: $("loginUser").value, password: $("loginPassword").value }),
  });
  showMain();
  await refreshAll();
}

async function logout() {
  await api("/api/auth/logout", { method: "POST" });
  state.user = null;
  showLogin();
}

function showLogin() {
  $("loginView").classList.remove("hidden");
  $("mainView").classList.add("hidden");
}

function showMain() {
  $("loginView").classList.add("hidden");
  $("mainView").classList.remove("hidden");
  $("userInfo").textContent = `${state.user.displayName || state.user.userName} / ${state.user.role}`;
}

function setTab(tab) {
  state.activeTab = tab;
  document.querySelectorAll("aside button[data-tab]").forEach((button) => {
    button.classList.toggle("active", button.dataset.tab === tab);
  });
  document.querySelectorAll(".tab").forEach((panel) => panel.classList.add("hidden"));
  $(`${tab}Tab`).classList.remove("hidden");
  $("pageTitle").textContent = { builds: "打包任务", projects: "项目配置", workers: "Worker", audit: "审计日志" }[tab];
}

async function refreshAll() {
  const [projects, configs, jobs, workers] = await Promise.all([
    api("/api/projects"),
    api("/api/configs"),
    api("/api/builds"),
    api("/api/workers"),
  ]);
  state.projects = projects;
  state.configs = configs;
  state.jobs = jobs;
  renderProjects();
  renderConfigsSelects();
  renderJobs();
  renderWorkers(workers);
  if (state.activeTab === "audit") await refreshAudit();
  if (state.selectedJobId) await showJob(state.selectedJobId);
}

async function refreshJobsSoft() {
  if (!state.user || state.activeTab !== "builds") return;
  try {
    state.jobs = await api("/api/builds");
    renderJobs();
    if (state.selectedJobId) await showJob(state.selectedJobId);
  } catch {
    // 登录过期时下一次手动刷新会提示。
  }
}

async function createProject(event) {
  event.preventDefault();
  await api("/api/projects", {
    method: "POST",
    body: JSON.stringify({
      name: $("projectName").value,
      repositoryUrl: $("projectRepo").value,
      defaultBranch: $("projectBranch").value,
      allowedBranches: $("projectAllowedBranches").value.split(",").map((item) => item.trim()).filter(Boolean),
      workspaceRoot: $("projectWorkspace").value,
      artifactsRoot: $("projectArtifacts").value,
      description: $("projectDescription").value,
    }),
  });
  $("projectForm").reset();
  $("projectBranch").value = "main";
  $("projectAllowedBranches").value = "main";
  $("projectWorkspace").value = "~/UnityBuildWorkspace";
  $("projectArtifacts").value = "~/UnityBuildArtifacts";
  await refreshAll();
}

async function createConfig(event) {
  event.preventDefault();
  await api("/api/configs", {
    method: "POST",
    body: JSON.stringify({
      projectId: $("configProject").value,
      name: $("configName").value,
      configPath: $("configPath").value,
      allowMcpBuild: $("configAllowMcp").checked,
    }),
  });
  $("configForm").reset();
  $("configAllowMcp").checked = false;
  await refreshAll();
}

async function startBuild(event) {
  event.preventDefault();
  const job = await api("/api/builds", {
    method: "POST",
    body: JSON.stringify({
      projectId: $("buildProject").value,
      configId: $("buildConfig").value,
      branch: $("buildBranch").value || null,
      buildNumber: $("buildNumber").value || null,
      dryRun: $("dryRun").checked,
      skipGit: $("skipGit").checked,
      skipUnity: $("skipUnity").checked,
      skipXcode: $("skipXcode").checked,
      allowNonMac: $("allowNonMac").checked,
      notes: $("buildNotes").value,
    }),
  });
  state.selectedJobId = job.id;
  await refreshAll();
}

async function cancelJob(jobId) {
  await api(`/api/builds/${jobId}/cancel`, { method: "POST" });
  await refreshAll();
}

function renderProjects() {
  $("projectsList").innerHTML = state.projects.map((project) => {
    const configs = state.configs.filter((config) => config.projectId === project.id);
    return `<article class="item">
      <header><strong>${escapeHtml(project.name)}</strong><span class="status">${project.enabled ? "Enabled" : "Disabled"}</span></header>
      <div class="muted">${escapeHtml(project.repositoryUrl)} [${escapeHtml(project.defaultBranch)}]</div>
      <div>Workspace: ${escapeHtml(project.workspaceRoot)}</div>
      <div>Artifacts: ${escapeHtml(project.artifactsRoot)}</div>
      <div class="muted">配置：${configs.map((config) => escapeHtml(config.name)).join("，") || "暂无"}</div>
    </article>`;
  }).join("");
}

function renderConfigsSelects() {
  const projectOptions = state.projects.map((project) => `<option value="${project.id}">${escapeHtml(project.name)}</option>`).join("");
  $("buildProject").innerHTML = projectOptions;
  $("configProject").innerHTML = projectOptions;
  renderBuildConfigs();
}

function renderBuildConfigs() {
  const projectId = $("buildProject").value;
  const configs = state.configs.filter((config) => config.projectId === projectId);
  $("buildConfig").innerHTML = configs.map((config) => `<option value="${config.id}">${escapeHtml(config.name)}</option>`).join("");
}

function renderJobs() {
  $("jobsList").innerHTML = state.jobs.map((job) => {
    const project = state.projects.find((item) => item.id === job.projectId);
    const config = state.configs.find((item) => item.id === job.configId);
    return `<article class="item">
      <header>
        <strong>${escapeHtml(project?.name || job.projectId)} / ${escapeHtml(config?.name || job.configId)}</strong>
        <span class="status ${job.status}">${job.status}</span>
      </header>
      <div class="muted">${new Date(job.createdAt).toLocaleString()} · ${escapeHtml(job.branch)} · build ${escapeHtml(job.buildNumber)} · ${job.source}</div>
      <button class="secondary" onclick="showJob('${job.id}')">查看</button>
      ${(job.status === "Queued" || job.status === "Running") ? `<button class="danger" onclick="cancelJob('${job.id}')">取消</button>` : ""}
    </article>`;
  }).join("");
}

async function showJob(jobId) {
  state.selectedJobId = jobId;
  const [job, log, artifacts] = await Promise.all([
    api(`/api/builds/${jobId}`),
    fetch(`/api/builds/${jobId}/log?lines=500`, { credentials: "include" }).then((response) => response.text()),
    api(`/api/builds/${jobId}/artifacts`),
  ]);
  $("jobDetail").innerHTML = [
    ["状态", job.status],
    ["分支", job.branch],
    ["Build", job.buildNumber],
    ["Worker", job.workerId || "-"],
    ["开始", job.startedAt ? new Date(job.startedAt).toLocaleString() : "-"],
    ["结束", job.finishedAt ? new Date(job.finishedAt).toLocaleString() : "-"],
    ["dry-run", job.dryRun ? "是" : "否"],
    ["错误", job.error || "-"],
  ].map(([k, v]) => `<div><strong>${k}</strong><br>${escapeHtml(String(v))}</div>`).join("");
  $("jobLog").textContent = log;
  $("artifactsList").innerHTML = artifacts.map((artifact) => `<article class="item">
    <strong>${escapeHtml(artifact.type)}</strong>
    <div class="muted">${escapeHtml(artifact.path)}</div>
    <a href="/api/artifacts/${artifact.id}/download" target="_blank">下载</a>
  </article>`).join("");
}

function renderWorkers(workers) {
  $("workersList").innerHTML = workers.map((worker) => `<article class="item">
    <header><strong>${escapeHtml(worker.name)}</strong><span class="status ${worker.status}">${worker.status}</span></header>
    <div>Host: ${escapeHtml(worker.hostName)}</div>
    <div>Current Job: ${escapeHtml(worker.currentJobId || "-")}</div>
    <div class="muted">Last Seen: ${new Date(worker.lastSeenAt).toLocaleString()}</div>
  </article>`).join("");
}

async function refreshAudit() {
  const audit = await api("/api/audit");
  $("auditList").innerHTML = audit.map((item) => `<article class="item">
    <strong>${escapeHtml(item.action)}</strong>
    <div>${escapeHtml(item.userName)} · ${escapeHtml(item.targetType)}:${escapeHtml(item.targetId)}</div>
    <div class="muted">${new Date(item.createdAt).toLocaleString()} · ${escapeHtml(item.details)}</div>
  </article>`).join("");
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (char) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#039;",
  }[char]));
}

init();
