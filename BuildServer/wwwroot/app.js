const state = {
  user: null,
  projects: [],
  configs: [],
  jobs: [],
  settings: null,
  manualConfigPath: "",
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
  if (response.status === 401) throw new Error(await readErrorMessage(response) || "未登录或登录已过期");
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

async function fetchText(path) {
  const response = await fetch(path, { credentials: "include" });
  if (response.status === 401) throw new Error(await readErrorMessage(response) || "未登录或登录已过期");
  if (!response.ok) throw new Error(await readErrorMessage(response));
  return response.text();
}

function showError(error) {
  const message = error instanceof Error ? error.message : String(error || "");
  if (message) showMessage(message, "error");
}

function showMessage(message, type = "info") {
  const element = $("globalMessage");
  if (!element) {
    alert(message);
    return;
  }

  element.textContent = message;
  element.className = `toast ${type === "error" ? "error" : ""}`.trim();
}

function clearMessage() {
  const element = $("globalMessage");
  if (!element) return;
  element.textContent = "";
  element.className = "toast hidden";
}

function showLoginError(error) {
  const element = $("loginError");
  element.textContent = error instanceof Error ? error.message : String(error || "");
  element.classList.remove("hidden");
}

function clearLoginError() {
  $("loginError").textContent = "";
  $("loginError").classList.add("hidden");
}

function setButtonBusy(id, busy, busyText = "处理中...") {
  const button = $(id);
  if (!button) return;
  if (!button.dataset.defaultText) {
    button.dataset.defaultText = button.textContent;
  }

  button.disabled = busy;
  button.textContent = busy ? busyText : button.dataset.defaultText;
}

async function init() {
  bindEvents();
  setConfigFileDefaults();
  toggleConfigFileFields();
  togglePlatformFields();
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
  $("jobsList").addEventListener("click", handleJobsListClick);
  $("jobModalClose").addEventListener("click", closeJobModal);
  $("jobModal").addEventListener("click", (event) => {
    if (event.target === $("jobModal")) closeJobModal();
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isJobModalOpen()) closeJobModal();
  });
  $("configCreateFile").addEventListener("change", toggleConfigFileFields);
  $("configBuildPlatform").addEventListener("change", () => {
    fillConfigFileDefaults({ forceFileName: true });
    togglePlatformFields();
  });
  $("configProject").addEventListener("change", () => {
    const project = state.projects.find((item) => item.id === $("configProject").value);
    if (project?.defaultBuildPlatform) {
      $("configBuildPlatform").value = project.defaultBuildPlatform;
    }
    fillConfigFileDefaults({ forceFileName: true });
  });
  $("configName").addEventListener("input", fillConfigFileDefaults);
  $("configFileName").addEventListener("input", updateConfigPathPreview);
  $("configPath").addEventListener("input", () => {
    if (!$("configCreateFile").checked) {
      state.manualConfigPath = $("configPath").value;
    }
  });
  document.querySelectorAll("aside button[data-tab]").forEach((button) => {
    button.addEventListener("click", () => setTab(button.dataset.tab));
  });
  $("buildProject").addEventListener("change", renderBuildConfigs);
}

async function login(event) {
  event.preventDefault();
  clearLoginError();
  setButtonBusy("loginBtn", true, "登录中...");
  try {
    state.user = await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ userName: $("loginUser").value, password: $("loginPassword").value }),
    });
    showMain();
    await refreshAll();
  } catch (error) {
    showLoginError(error);
  } finally {
    setButtonBusy("loginBtn", false);
  }
}

async function logout() {
  await api("/api/auth/logout", { method: "POST" });
  state.user = null;
  closeJobModal();
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
  $("pageTitle").textContent = { builds: "打包任务", projects: "项目配置", workers: "Worker", audit: "审计日志", help: "填写说明" }[tab];
}

async function refreshAll(options = {}) {
  const showSuccess = options.showSuccess ?? true;
  const throwOnError = options.throwOnError ?? false;
  clearMessage();
  setButtonBusy("refreshBtn", true, "刷新中...");
  try {
    const [projects, configs, jobs, workers, settings] = await Promise.all([
      api("/api/projects"),
      api("/api/configs"),
      api("/api/builds"),
      api("/api/workers"),
      api("/api/settings"),
    ]);
    state.projects = projects;
    state.configs = configs;
    state.jobs = jobs;
    state.settings = settings;
    renderProjects();
    renderConfigsSelects();
    renderJobs();
    renderMetrics();
    renderWorkers(workers);
    if (state.activeTab === "audit") await refreshAudit();
    if (state.selectedJobId && isJobModalOpen()) await refreshJobModal(state.selectedJobId);
    if (showSuccess) {
      showMessage("数据已刷新。");
    }
  } catch (error) {
    showError(error);
    if (throwOnError) {
      throw error;
    }
  } finally {
    setButtonBusy("refreshBtn", false);
  }
}

async function refreshJobsSoft() {
  if (!state.user || state.activeTab !== "builds") return;
  try {
    state.jobs = await api("/api/builds");
    renderJobs();
    renderMetrics();
    if (state.selectedJobId && isJobModalOpen()) await refreshJobModal(state.selectedJobId);
  } catch {
    // 登录过期时下一次手动刷新会提示。
  }
}

async function createProject(event) {
  event.preventDefault();
  clearMessage();
  setButtonBusy("projectSaveBtn", true, "保存中...");
  try {
    await api("/api/projects", {
      method: "POST",
      body: JSON.stringify({
        name: $("projectName").value,
        repositoryUrl: $("projectRepo").value,
        defaultBranch: $("projectBranch").value,
        allowedBranches: $("projectAllowedBranches").value.split(",").map((item) => item.trim()).filter(Boolean),
        workspaceRoot: $("projectWorkspace").value,
        artifactsRoot: $("projectArtifacts").value,
        defaultBuildPlatform: $("projectDefaultPlatform").value,
        description: $("projectDescription").value,
      }),
    });
    $("projectForm").reset();
    $("projectBranch").value = "main";
    $("projectAllowedBranches").value = "main";
    $("projectWorkspace").value = "~/UnityBuildWorkspace";
    $("projectArtifacts").value = "~/UnityBuildArtifacts";
    $("projectDefaultPlatform").value = "ios";
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage("项目已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("projectSaveBtn", false);
  }
}

async function createConfig(event) {
  event.preventDefault();
  clearMessage();
  setButtonBusy("configSaveBtn", true, "保存中...");
  try {
    const createFile = $("configCreateFile").checked;
    if (createFile) {
      await api("/api/config-files", {
        method: "POST",
        body: JSON.stringify({
          projectId: $("configProject").value,
          name: $("configName").value,
          buildPlatform: $("configBuildPlatform").value,
          fileName: $("configFileName").value || null,
          projectDirectoryName: $("configProjectDirectoryName").value || null,
          unityProjectRelativePath: $("configUnityRelativePath").value || ".",
          unityVersion: $("configUnityVersion").value || null,
          unityExecutablePath: $("configUnityExecutablePath").value || null,
          productName: $("configProductName").value || null,
          bundleIdentifier: $("configBundleIdentifier").value || null,
          teamId: $("configTeamId").value || null,
          iosDeploymentTarget: $("configIosDeploymentTarget").value || null,
          buildNumber: $("configBuildNumber").value || "1",
          bundleVersion: $("configBundleVersion").value || "1.0.0",
          exportMethod: $("configExportMethod").value,
          signingStyle: $("configSigningStyle").value,
          syncBundleVersionFromUnity: $("configSyncUnityVersion").checked,
          autoIncrementBuildNumber: $("configAutoIncrementBuild").checked,
          allowProvisioningUpdates: $("configAllowProvisioningUpdates").checked,
          copyArchiveToOrganizer: $("configCopyArchiveToOrganizer").checked,
          androidBuildFormat: $("configAndroidBuildFormat").value,
          androidOutputDirectory: $("configAndroidOutputDirectory").value || null,
          apkOutputPath: $("configApkOutputPath").value || null,
          aabOutputPath: $("configAabOutputPath").value || null,
          androidMinSdkVersion: $("configAndroidMinSdkVersion").value || null,
          androidTargetSdkVersion: $("configAndroidTargetSdkVersion").value || null,
          androidKeystoreName: $("configAndroidKeystoreName").value || null,
          androidKeystorePass: $("configAndroidKeystorePass").value || null,
          androidKeyaliasName: $("configAndroidKeyaliasName").value || null,
          androidKeyaliasPass: $("configAndroidKeyaliasPass").value || null,
          googlePlayUploadEnabled: $("configGooglePlayUploadEnabled").checked,
          googlePlayPackageName: $("configGooglePlayPackageName").value || null,
          googlePlayServiceAccountJsonPath: $("configGooglePlayServiceAccountJsonPath").value || null,
          googlePlayTrack: $("configGooglePlayTrack").value,
          googlePlayReleaseStatus: $("configGooglePlayReleaseStatus").value,
          googlePlayReleaseName: $("configGooglePlayReleaseName").value || null,
          googlePlayUploadArtifact: $("configGooglePlayUploadArtifact").value,
          googlePlayChangesNotSentForReview: $("configGooglePlayChangesNotSentForReview").checked,
          googlePlayUserFraction: parseOptionalNumber($("configGooglePlayUserFraction").value),
          overwriteExisting: $("configOverwriteFile").checked,
          allowMcpBuild: $("configAllowMcp").checked,
        }),
      });
    } else {
      await api("/api/configs", {
        method: "POST",
        body: JSON.stringify({
          projectId: $("configProject").value,
          name: $("configName").value,
          buildPlatform: $("configBuildPlatform").value,
          configPath: $("configPath").value,
          allowMcpBuild: $("configAllowMcp").checked,
        }),
      });
    }
    $("configForm").reset();
    state.manualConfigPath = "";
    setConfigFileDefaults();
    toggleConfigFileFields();
    togglePlatformFields();
    $("configAllowMcp").checked = false;
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage("配置已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("configSaveBtn", false);
  }
}

function toggleConfigFileFields() {
  const createFile = $("configCreateFile").checked;
  if (createFile) {
    state.manualConfigPath = $("configPath").value;
  }

  $("configFileFields").classList.toggle("hidden", !createFile);
  $("configPath").disabled = createFile;
  $("configPath").required = !createFile;
  if (createFile) {
    fillConfigFileDefaults();
    updateConfigPathPreview();
  } else {
    $("configPath").value = state.manualConfigPath;
  }
}

function fillConfigFileDefaults(options = {}) {
  const project = state.projects.find((item) => item.id === $("configProject").value);
  const configName = $("configName").value.trim() || "release";
  const platform = $("configBuildPlatform").value || project?.defaultBuildPlatform || "ios";
  if (!$("configBuildPlatform").value && project?.defaultBuildPlatform) {
    $("configBuildPlatform").value = project.defaultBuildPlatform;
  }

  if (options.forceFileName || !$("configFileName").value.trim()) {
    $("configFileName").value = `build-${platform}.${safeFilePart(configName)}.json`;
  }

  if (project && !$("configProjectDirectoryName").value.trim()) {
    $("configProjectDirectoryName").value = deriveRepoFolderName(project.repositoryUrl) || safeFilePart(project.name);
  }

  if (project && !$("configProductName").value.trim()) {
    $("configProductName").value = project.name;
  }

  togglePlatformFields();
  updateConfigPathPreview();
}

function updateConfigPathPreview() {
  if (!$("configCreateFile").checked) return;

  const configName = $("configName").value.trim() || "release";
  const platform = $("configBuildPlatform").value || "ios";
  const rawFileName = $("configFileName").value.trim() || `build-${platform}.${safeFilePart(configName)}.json`;
  const fileName = rawFileName.toLowerCase().endsWith(".json") ? rawFileName : `${rawFileName}.json`;
  const root = state.settings?.configRoot || "服务端配置目录";
  const normalizedRoot = root.replace(/[\\\/]$/, "");
  const separator = normalizedRoot.includes("\\") ? "\\" : "/";
  $("configPath").value = `${normalizedRoot}${separator}${fileName}`;
}

function setConfigFileDefaults() {
  $("configUnityRelativePath").value = ".";
  $("configBuildPlatform").value = "ios";
  $("configIosDeploymentTarget").value = "13.0";
  $("configBuildNumber").value = "1";
  $("configBundleVersion").value = "1.0.0";
  $("configExportMethod").value = "development";
  $("configSigningStyle").value = "automatic";
  $("configSyncUnityVersion").checked = true;
  $("configAutoIncrementBuild").checked = true;
  $("configAllowProvisioningUpdates").checked = true;
  $("configCopyArchiveToOrganizer").checked = true;
  $("configAndroidBuildFormat").value = "aab";
  $("configGooglePlayTrack").value = "internal";
  $("configGooglePlayReleaseStatus").value = "draft";
  $("configGooglePlayUploadArtifact").value = "aab";
  $("configGooglePlayUploadEnabled").checked = false;
  $("configGooglePlayChangesNotSentForReview").checked = false;
  $("configOverwriteFile").checked = false;
}

function togglePlatformFields() {
  const platform = $("configBuildPlatform").value || "ios";
  $("iosConfigFields").classList.toggle("hidden", platform !== "ios");
  $("androidConfigFields").classList.toggle("hidden", platform !== "android");
}

function deriveRepoFolderName(repositoryUrl) {
  const cleaned = String(repositoryUrl || "").replace(/\/+$/, "");
  const index = Math.max(cleaned.lastIndexOf("/"), cleaned.lastIndexOf(":"));
  const name = index >= 0 ? cleaned.slice(index + 1) : cleaned;
  return name.replace(/\.git$/i, "");
}

function safeFilePart(value) {
  return String(value || "config")
    .trim()
    .replace(/[^a-zA-Z0-9_-]+/g, "-")
    .replace(/^-+|-+$/g, "") || "config";
}

function parseOptionalNumber(value) {
  const text = String(value || "").trim();
  if (!text) return null;
  const number = Number(text);
  if (!Number.isFinite(number)) {
    throw new Error("User Fraction 必须是数字，例如 0.1 或 1。");
  }
  return number;
}

async function startBuild(event) {
  event.preventDefault();
  clearMessage();
  setButtonBusy("buildSubmitBtn", true, "提交中...");
  try {
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
    await refreshAll({ showSuccess: false, throwOnError: true });
    await openJobModal(job.id);
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("buildSubmitBtn", false);
  }
}

async function cancelJob(jobId) {
  await api(`/api/builds/${jobId}/cancel`, { method: "POST" });
  await refreshAll();
  if (state.selectedJobId === jobId && isJobModalOpen()) {
    await refreshJobModal(jobId);
  }
}

async function handleJobsListClick(event) {
  const viewButton = event.target.closest("[data-view-job-id]");
  if (viewButton) {
    await openJobModal(viewButton.dataset.viewJobId);
    return;
  }

  const cancelButton = event.target.closest("[data-cancel-job-id]");
  if (cancelButton) {
    await cancelJob(cancelButton.dataset.cancelJobId);
  }
}

function renderProjects() {
  if (state.projects.length === 0) {
    $("projectsList").innerHTML = `<article class="item muted">暂无项目。请先在左侧表单新增项目。</article>`;
    return;
  }

  $("projectsList").innerHTML = state.projects.map((project) => {
    const configs = state.configs.filter((config) => config.projectId === project.id);
    return `<article class="item">
      <header><strong>${escapeHtml(project.name)}</strong><span class="status">${project.enabled ? "Enabled" : "Disabled"}</span></header>
      <div class="muted">${escapeHtml(project.repositoryUrl)} [${escapeHtml(project.defaultBranch)}] / ${platformBadge(project.defaultBuildPlatform || "ios")}</div>
      <div>Workspace: ${escapeHtml(project.workspaceRoot)}</div>
      <div>Artifacts: ${escapeHtml(project.artifactsRoot)}</div>
      <div class="muted">配置: ${configs.map((config) => escapeHtml(config.name)).join(", ") || "暂无"}</div>
    </article>`;
  }).join("");
}

function renderConfigsSelects() {
  const projectOptions = state.projects.map((project) => `<option value="${escapeHtml(project.id)}">${escapeHtml(project.name)}</option>`).join("");
  $("buildProject").innerHTML = projectOptions;
  $("configProject").innerHTML = projectOptions;
  const project = state.projects.find((item) => item.id === $("configProject").value);
  if (project?.defaultBuildPlatform) {
    $("configBuildPlatform").value = project.defaultBuildPlatform;
  }
  fillConfigFileDefaults();
  renderBuildConfigs();
}

function renderBuildConfigs() {
  const projectId = $("buildProject").value;
  const configs = state.configs.filter((config) => config.projectId === projectId);
  $("buildConfig").innerHTML = configs.map((config) => `<option value="${escapeHtml(config.id)}">${escapeHtml(config.name)} / ${escapeHtml(config.buildPlatform || "ios")}</option>`).join("");
}

function renderJobs() {
  if (state.jobs.length === 0) {
    $("jobsList").innerHTML = `<article class="item muted">暂无任务。选择项目和配置后即可发起打包。</article>`;
    return;
  }

  $("jobsList").innerHTML = state.jobs.map((job) => {
    const project = state.projects.find((item) => item.id === job.projectId);
    const config = state.configs.find((item) => item.id === job.configId);
    return `<article class="item">
      <header>
        <strong>${escapeHtml(project?.name || job.projectId)} / ${escapeHtml(config?.name || job.configId)} / ${platformBadge(job.buildPlatform || config?.buildPlatform || "ios")}</strong>
        <span class="status ${escapeHtml(job.status)}">${escapeHtml(job.status)}</span>
      </header>
      <div class="muted">${new Date(job.createdAt).toLocaleString()} | ${escapeHtml(job.branch)} | build ${escapeHtml(job.buildNumber)} | ${escapeHtml(job.source)}</div>
      <div class="item-actions">
        <button class="secondary" type="button" data-view-job-id="${escapeHtml(job.id)}">查看</button>
        ${(job.status === "Queued" || job.status === "Running") ? `<button class="danger" type="button" data-cancel-job-id="${escapeHtml(job.id)}">取消</button>` : ""}
      </div>
    </article>`;
  }).join("");
}

function renderMetrics() {
  const runningJobs = state.jobs.filter((job) => job.status === "Queued" || job.status === "Running").length;
  $("metricProjects").textContent = String(state.projects.length);
  $("metricConfigs").textContent = String(state.configs.length);
  $("metricRunning").textContent = String(runningJobs);
  $("metricJobs").textContent = String(state.jobs.length);
}

async function openJobModal(jobId) {
  state.selectedJobId = jobId;
  $("jobModal").classList.remove("hidden");
  $("jobModal").setAttribute("aria-hidden", "false");
  $("jobModalTitle").textContent = "任务详情";
  $("jobModalSubTitle").textContent = jobId;
  $("jobModalDetail").innerHTML = `<div><strong>状态</strong><br>加载中...</div>`;
  $("jobModalArtifacts").innerHTML = `<article class="item muted">正在加载产物...</article>`;
  $("jobModalLog").textContent = "正在加载日志...";
  await refreshJobModal(jobId);
}

function closeJobModal() {
  $("jobModal").classList.add("hidden");
  $("jobModal").setAttribute("aria-hidden", "true");
  state.selectedJobId = null;
}

function isJobModalOpen() {
  return !$("jobModal").classList.contains("hidden");
}

async function refreshJobModal(jobId) {
  const [job, log, artifacts] = await Promise.all([
    api(`/api/builds/${jobId}`),
    fetchText(`/api/builds/${jobId}/log?full=true`),
    api(`/api/builds/${jobId}/artifacts`),
  ]);

  $("jobModalTitle").textContent = "任务详情";
  $("jobModalSubTitle").textContent = `${job.id} / ${new Date(job.createdAt).toLocaleString()}`;
  $("jobModalDetail").innerHTML = [
    ["状态", job.status],
    ["平台", job.buildPlatform || "ios"],
    ["分支", job.branch],
    ["Build Number", job.buildNumber],
    ["Worker", job.workerId || "-"],
    ["开始时间", job.startedAt ? new Date(job.startedAt).toLocaleString() : "-"],
    ["结束时间", job.finishedAt ? new Date(job.finishedAt).toLocaleString() : "-"],
    ["dry-run", job.dryRun ? "是" : "否"],
    ["错误信息", job.error || "-"],
  ].map(([key, value]) => `<div><strong>${key}</strong><br>${escapeHtml(String(value))}</div>`).join("");

  $("jobModalLog").textContent = log || "暂无日志";
  $("jobModalArtifacts").innerHTML = artifacts.length
    ? artifacts.map((artifact) => `<article class="item artifact-item">
        <div>
          <strong>${escapeHtml(artifact.type)}</strong>
          <div class="muted">${escapeHtml(artifact.path)}</div>
        </div>
        <a class="download-link" href="/api/artifacts/${escapeHtml(artifact.id)}/download" target="_blank" rel="noopener">下载</a>
      </article>`).join("")
    : `<article class="item muted">暂无可下载产物</article>`;
}

function platformBadge(platform) {
  const value = String(platform || "ios");
  return `<span class="platform ${escapeHtml(value)}">${escapeHtml(value)}</span>`;
}

function renderWorkers(workers) {
  if (workers.length === 0) {
    $("workersList").innerHTML = `<article class="item muted">暂无 Worker 心跳。</article>`;
    return;
  }

  $("workersList").innerHTML = workers.map((worker) => `<article class="item">
    <header><strong>${escapeHtml(worker.name)}</strong><span class="status ${escapeHtml(worker.status)}">${escapeHtml(worker.status)}</span></header>
    <div>Host: ${escapeHtml(worker.hostName)}</div>
    <div>Current Job: ${escapeHtml(worker.currentJobId || "-")}</div>
    <div class="muted">Last Seen: ${new Date(worker.lastSeenAt).toLocaleString()}</div>
  </article>`).join("");
}

async function refreshAudit() {
  const audit = await api("/api/audit");
  if (audit.length === 0) {
    $("auditList").innerHTML = `<article class="item muted">暂无审计记录。</article>`;
    return;
  }

  $("auditList").innerHTML = audit.map((item) => `<article class="item">
    <strong>${escapeHtml(item.action)}</strong>
    <div>${escapeHtml(item.userName)} | ${escapeHtml(item.targetType)}:${escapeHtml(item.targetId)}</div>
    <div class="muted">${new Date(item.createdAt).toLocaleString()} | ${escapeHtml(item.details)}</div>
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
