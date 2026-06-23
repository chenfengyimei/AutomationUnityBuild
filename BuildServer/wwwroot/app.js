const state = {
  user: null,
  projects: [],
  configs: [],
  jobs: [],
  users: [],
  settings: null,
  manualConfigPath: "",
  selectedJobId: null,
  editingConfigId: null,
  editingConfigPath: "",
  activeTab: "builds",
  events: null,
};

const $ = (id) => document.getElementById(id);

const CONFIG_FIELD_HELP = {
  configProject: {
    title: "项目",
    subtitle: "这份配置属于哪个 Unity 项目。",
    body: "先选项目，再创建配置。一个项目下面可以有多份配置，比如 iOS 测试、iOS 上架、Android AAB、Android APK。",
    tips: ["如果这里没有项目，先在左侧“新增项目”里创建。", "配置保存后，打包任务会按项目和配置组合来选择。"]
  },
  configName: {
    title: "配置名",
    subtitle: "给这套打包参数起个容易看懂的名字。",
    body: "建议写用途，不要只写随便的字母。比如 ios-testflight、ios-release、android-googleplay、android-debug。",
    tips: ["这个名字会出现在配置列表和任务列表里。", "同一个项目可以有多个不同配置名。"]
  },
  configBuildPlatform: {
    title: "打包平台",
    subtitle: "决定这份配置是打 iOS 还是 Android。",
    body: "选 iOS 时会显示 Xcode、签名、App Store Connect 相关字段；选 Android 时会显示 APK/AAB、Keystore、Google Play 相关字段。",
    tips: ["Mac 可以打 iOS 和 Android。", "Windows 只能打 Android，不能打 iOS。"]
  },
  configPath: {
    title: "配置文件路径",
    subtitle: "已有 JSON 配置文件的位置。",
    body: "如果你已经手动准备好了配置 JSON，就在这里填它在 BuildServer 这台机器上的路径。勾选“生成新的配置文件”后，这里会自动预览新文件保存位置。",
    tips: ["路径是运行 BuildServer 的机器上的路径，不是你浏览器电脑的路径。", "如果不想手改 JSON，建议勾选“生成新的配置文件”。"]
  },
  configCreateFile: {
    title: "生成新的配置文件",
    subtitle: "让网页帮你生成 JSON。",
    body: "开启后，你在下面表单里填的信息会被保存成一个新的 JSON 配置文件，同时登记到 BuildServer 里。以后打包直接选这个配置即可。",
    tips: ["推荐新手开启。", "不开启时，必须自己准备已有配置文件路径。"]
  },
  configFileName: {
    title: "文件名",
    subtitle: "新生成的 JSON 配置文件叫什么。",
    body: "只填文件名，不要填目录。系统会保存到 BuildServer 允许的配置目录里。",
    tips: ["推荐格式：build-ios.release.json 或 build-android.googleplay.json。", "如果不写 .json，系统会自动补上。"]
  },
  configProjectDirectoryName: {
    title: "仓库目录名",
    subtitle: "Git clone 到工作区后的文件夹名。",
    body: "工具会把仓库拉到 Workspace Root 下面的这个文件夹里。留空时会从 Git 仓库地址自动推断。",
    tips: ["例如仓库是 SaveCatCat.git，这里一般就是 SaveCatCat。", "不要填完整路径，只填一个文件夹名。"]
  },
  configUnityRelativePath: {
    title: "Unity 工程相对路径",
    subtitle: "Unity 项目在仓库里的位置。",
    body: "如果仓库根目录就是 Unity 工程，填英文句点 .。如果 Unity 工程在子目录，比如 Client，就填 Client。",
    tips: ["这个目录里面必须能看到 Assets 和 ProjectSettings。", "不要填 build、Builds、XcodeProject 这类输出目录。"]
  },
  configUnityVersion: {
    title: "Unity 版本",
    subtitle: "Mac/Windows 打包机上安装的 Unity Editor 版本。",
    body: "如果 Unity 是 Unity Hub 默认安装，通常填版本号就行，比如 2022.3.62f2c1。工具会自动拼出 Unity 可执行文件路径。",
    tips: ["Mac 可用 ls /Applications/Unity/Hub/Editor 查看。", "如果填了 Unity 完整路径，这个版本号主要用于记录。"]
  },
  configUnityExecutablePath: {
    title: "Unity 完整路径",
    subtitle: "特殊安装位置才需要填。",
    body: "Unity 没装在默认 Hub 目录时，填 Unity 可执行文件的完整路径。普通 Hub 安装可以留空。",
    tips: ["Mac 常见路径类似 /Applications/Unity/Hub/Editor/版本号/Unity.app/Contents/MacOS/Unity。", "Windows Android 节点可填 Unity.exe 的完整路径。"]
  },
  configProductName: {
    title: "Product Name",
    subtitle: "App 显示名称。",
    body: "这是打包时传给 Unity/Xcode/Android 的产品名，通常就是游戏名。",
    tips: ["它会影响产物文件名、Xcode Archive 展示名称等。", "真正商店里显示什么，还可能受 App Store Connect 或 Google Play 后台配置影响。"]
  },
  configBundleIdentifier: {
    title: "Bundle Identifier",
    subtitle: "应用包名，也就是唯一身份。",
    body: "iOS 和 Android 都需要它。一般格式是 com.company.game，比如 com.tapplus.savacat。",
    tips: ["必须和 Apple Developer / App Store Connect / Google Play 上创建的 App 保持一致。", "不要包含空格、中文或下划线。"]
  },
  configBuildNumber: {
    title: "Build Number",
    subtitle: "构建号，每次上传商店通常都要变大。",
    body: "同一个版本号下面，Build Number 用来区分第几次构建。App Store/TestFlight 和 Google Play 都不喜欢重复的构建号。",
    tips: ["建议填纯数字，例如 1、2、100。", "开启自动 +1 后，每次正式打包会自动递增。"]
  },
  configBundleVersion: {
    title: "Bundle Version",
    subtitle: "面向用户看的版本号。",
    body: "常见格式是 1.0.0、1.2.3。它通常对应 App 版本，而 Build Number 对应这一版里的第几次构建。",
    tips: ["如果开启“版本号同步 Unity 项目”，这里主要是记录值。", "关闭同步时，工具会用这里的值覆盖 Unity 项目版本号。"]
  },
  configSyncUnityVersion: {
    title: "版本号同步 Unity 项目",
    subtitle: "以 Unity Player Settings 里的版本号为准。",
    body: "开启后，打包时不会强制改 Unity 的 Bundle Version，而是读取 Unity 项目当前版本号。这样版本号由 Unity 项目统一维护。",
    tips: ["推荐开启，避免网页配置和 Unity 项目版本不一致。", "如果想临时强制版本号，再关闭它。"]
  },
  configAutoIncrementBuild: {
    title: "Build Number 自动 +1",
    subtitle: "正式打包时自动递增构建号。",
    body: "开启后，每次正式打包前会把 Build Number 加 1，减少上传商店时报构建号重复的概率。",
    tips: ["dry-run 不会真的递增。", "Build Number 必须是纯数字才能自动 +1。"]
  },
  configTeamId: {
    title: "Apple Team ID",
    subtitle: "Apple Developer 团队 ID。",
    body: "这是 10 位字母数字，不是公司名。Xcode 签名时需要它来知道用哪个 Apple 开发者团队。",
    tips: ["可以在 Apple Developer Membership 或 Xcode Accounts 里查看。", "例如 ABCDE12345，不要填 FT Entertainment Limited 这种公司名。"]
  },
  configIosDeploymentTarget: {
    title: "iOS Deployment Target",
    subtitle: "最低支持的 iOS 系统版本。",
    body: "比如填 13.0，表示这个包最低支持 iOS 13。广告 SDK、支付 SDK 等第三方库经常会要求某个最低版本。",
    tips: ["不知道时可以先填 13.0。", "如果 Xcode 报 SDK 最低版本问题，就需要调高这里。"]
  },
  configExportMethod: {
    title: "Export Method",
    subtitle: "决定 iOS 包按什么用途导出。",
    body: "development 是开发调试；ad-hoc 是给登记过 UDID 的设备测试；app-store 是上传 TestFlight/App Store；enterprise 是企业证书分发。",
    tips: ["要上传 TestFlight 或 App Store，必须选 app-store。", "只是本地开发测试，通常选 development。"]
  },
  configSigningStyle: {
    title: "Signing Style",
    subtitle: "Xcode 签名方式。",
    body: "automatic 表示让 Xcode 自动找证书和描述文件；manual 表示你自己指定签名资料。",
    tips: ["新手和专用打包机建议先用 automatic。", "manual 通常要配 provisioningProfiles 等高级字段。"]
  },
  configAllowProvisioningUpdates: {
    title: "允许 Xcode 自动处理签名",
    subtitle: "允许 xcodebuild 自动更新签名相关资料。",
    body: "开启后，Xcode 可以自动处理一些证书、描述文件更新。前提是 Mac 当前用户已经登录 Xcode，并且账号有权限。",
    tips: ["使用 automatic 签名时建议开启。", "证书和描述文件必须装在运行 BuildServer 的 macOS 用户下。"]
  },
  configCopyArchiveToOrganizer: {
    title: "复制 Archive 到 Organizer",
    subtitle: "让 Xcode Organizer 能看到本次归档。",
    body: "开启后，archive 成功后会复制到 Xcode 的 Organizer 目录。你打开 Xcode Organizer 时就能看到这个包。",
    tips: ["这只是方便查看和手动上传。", "如果要无人值守上传，还要开启 App Store Connect 自动上传。"]
  },
  configAppStoreConnectUploadEnabled: {
    title: "上传 App Store Connect/TestFlight",
    subtitle: "打包完成后自动上传 Apple 后台。",
    body: "开启后，工具会在 Xcode export 后自动把 archive 上传到 App Store Connect。上传成功后，构建会进入 TestFlight 处理队列。",
    tips: ["开启时 Export Method 必须是 app-store。", "上传成功不等于自动提交审核或自动发布。"]
  },
  configAppStoreConnectApiKeyPath: {
    title: "API Key .p8 路径",
    subtitle: "App Store Connect API Key 文件在打包机上的路径。",
    body: "这是 Apple 后台下载的 AuthKey_XXXXXXXXXX.p8 文件。必须放在 Mac 打包机本地安全目录，然后填本机路径。",
    tips: ["例如 /Users/buildbot/secrets/AuthKey_XXXXXXXXXX.p8。", "不要把 .p8 文件提交到 Git。"]
  },
  configAppStoreConnectApiKeyId: {
    title: "API Key ID",
    subtitle: "App Store Connect API Key 的 Key ID。",
    body: "创建 API Key 后，Apple 后台会显示 Key ID。它通常是 10 位左右的大写字母数字。",
    tips: ["它不是 Team ID。", "要和 .p8 文件属于同一个 API Key。"]
  },
  configAppStoreConnectApiIssuerId: {
    title: "Issuer ID",
    subtitle: "App Store Connect API 的 Issuer ID。",
    body: "在 App Store Connect 的 Users and Access / Integrations / App Store Connect API 页面可以看到。",
    tips: ["通常是 UUID 格式。", "Key ID、Issuer ID、.p8 三个要配套使用。"]
  },
  configAndroidBuildFormat: {
    title: "Android Build Format",
    subtitle: "Android 要输出 APK、AAB，还是两个都出。",
    body: "apk 适合本地安装测试；aab 是 Google Play 推荐/常用上传格式；both 会同时生成 APK 和 AAB。",
    tips: ["要上传 Google Play，通常选 aab。", "要发给别人直接安装，通常需要 apk。"]
  },
  configAndroidOutputDirectory: {
    title: "Android 输出目录",
    subtitle: "Android 产物放在哪个目录。",
    body: "留空时会自动放到本次打包产物目录下。只有你想固定输出位置时才需要填。",
    tips: ["路径属于实际执行打包的 Mac/Windows 节点。", "不确定就留空。"]
  },
  configApkOutputPath: {
    title: "APK 输出路径",
    subtitle: "指定 APK 文件完整路径。",
    body: "留空时工具会自动生成 APK 路径。只有你想固定文件名或固定目录时才填。",
    tips: ["只有 Android Build Format 包含 apk 时才会用到。", "一般留空更省心。"]
  },
  configAabOutputPath: {
    title: "AAB 输出路径",
    subtitle: "指定 AAB 文件完整路径。",
    body: "留空时工具会自动生成 AAB 路径。AAB 通常用于上传 Google Play。",
    tips: ["只有 Android Build Format 包含 aab 时才会用到。", "一般留空更省心。"]
  },
  configAndroidMinSdkVersion: {
    title: "Min SDK",
    subtitle: "Android 最低支持版本。",
    body: "这是 Android 的最低系统 API 等级。比如 23 表示最低 Android 6.0。",
    tips: ["不确定可以留空，使用 Unity 项目设置。", "如果 SDK 或商店要求最低版本，再填具体数字。"]
  },
  configAndroidTargetSdkVersion: {
    title: "Target SDK",
    subtitle: "Android 目标 API 等级。",
    body: "Google Play 每年可能要求新的 Target SDK。这里可以强制指定目标 API 等级。",
    tips: ["例如 35。", "不确定可以留空，使用 Unity 项目设置。"]
  },
  configAndroidKeystoreName: {
    title: "Keystore 路径",
    subtitle: "Android 正式签名文件路径。",
    body: "正式 APK/AAB 必须签名。这里填 keystore 文件在打包机上的完整路径。",
    tips: ["例如 /Users/buildbot/keys/game.keystore。", "不要把 keystore 提交到 Git。"]
  },
  configAndroidKeystorePass: {
    title: "Keystore 密码",
    subtitle: "打开 keystore 文件的密码。",
    body: "这是 Android 签名需要的敏感信息。BuildServer 会把它写入配置，所以配置目录要做好权限保护。",
    tips: ["不要公开给无关人员。", "日志和快照里会做基础脱敏，但配置文件本身仍要保护。"]
  },
  configAndroidKeyaliasName: {
    title: "Key Alias",
    subtitle: "keystore 里的具体签名别名。",
    body: "一个 keystore 里可能有多个 key，Key Alias 用来指定使用哪一个。",
    tips: ["常见名字可能是 release、game、upload。", "必须和创建 keystore 时的 alias 一致。"]
  },
  configAndroidKeyaliasPass: {
    title: "Key Alias 密码",
    subtitle: "具体签名 key 的密码。",
    body: "有些 keystore 的 alias 密码和 keystore 密码一样，有些不一样。这里按你的签名文件实际情况填写。",
    tips: ["如果填错，Android 签名阶段会失败。", "同样属于敏感信息。"]
  },
  configGooglePlayUploadEnabled: {
    title: "上传 Google Play",
    subtitle: "打包完成后自动上传 Google Play Console。",
    body: "开启后，工具会用 Google Play Developer API 上传 APK/AAB，并分配到你选择的测试轨或生产轨。",
    tips: ["第一次建议用 internal + draft。", "需要先配置 Service Account JSON。"]
  },
  configGooglePlayPackageName: {
    title: "Google Play Package",
    subtitle: "Google Play 上的应用包名。",
    body: "通常和 Bundle Identifier 一样，比如 com.company.game。留空时会尝试使用 Bundle Identifier。",
    tips: ["必须和 Google Play Console 里创建的 App 一致。", "填错会上传到不存在或错误的应用。"]
  },
  configGooglePlayServiceAccountJsonPath: {
    title: "Service Account JSON",
    subtitle: "Google Play 服务账号 JSON 文件路径。",
    body: "这是 Google Cloud/Play Console 授权用的 JSON 文件。必须放在实际打包机本地，并确保它有上传权限。",
    tips: ["例如 /Users/buildbot/secrets/google-play.json。", "不要提交到 Git。"]
  },
  configGooglePlayTrack: {
    title: "Track",
    subtitle: "上传到 Google Play 哪个轨道。",
    body: "internal 是内部测试，alpha/beta 是测试轨，production 是正式生产。",
    tips: ["第一次建议 internal。", "production 要格外谨慎。"]
  },
  configGooglePlayReleaseStatus: {
    title: "Release Status",
    subtitle: "Google Play 发布状态。",
    body: "draft 表示草稿，不会直接发出去；completed 表示完成发布；inProgress 通常配合灰度比例使用。",
    tips: ["第一次建议 draft。", "确认流程稳定后再考虑 completed 或 inProgress。"]
  },
  configGooglePlayReleaseName: {
    title: "Release Name",
    subtitle: "Google Play 这次 release 的名字。",
    body: "可以留空，也可以写一个内部好识别的名字，比如 1.2.3 build 45。",
    tips: ["主要用于后台识别。", "不影响 App 显示名称。"]
  },
  configGooglePlayUploadArtifact: {
    title: "上传产物",
    subtitle: "Google Play 上传 APK、AAB，还是两个都上传。",
    body: "通常上传 aab。选择 apk 或 both 时，Android Build Format 也必须包含对应产物。",
    tips: ["Google Play 新应用一般要求 AAB。", "如果这里选 aab，但前面只打 apk，会直接校验失败。"]
  },
  configGooglePlayChangesNotSentForReview: {
    title: "changesNotSentForReview",
    subtitle: "Google Play 提交时先不送审。",
    body: "开启后，上传改动可以先停留在后台，不立即送审。适合你想先上传，再由负责人手动检查和送审。",
    tips: ["不确定就保持关闭或配合 draft 使用。", "不同账号和应用状态下 Google Play 行为可能略有差异。"]
  },
  configGooglePlayUserFraction: {
    title: "User Fraction",
    subtitle: "灰度发布比例。",
    body: "填 0.1 到 1 之间的小数，比如 0.1 表示 10% 用户。内部测试或草稿通常不用填。",
    tips: ["只有某些 release status/track 场景才需要。", "不确定就留空。"]
  },
  configOverwriteFile: {
    title: "覆盖同名配置文件",
    subtitle: "允许覆盖已经存在的 JSON。",
    body: "开启后，如果目标配置文件名已经存在，会直接覆盖它。",
    tips: ["谨慎开启，避免把旧配置覆盖掉。", "如果只是新增配置，建议换一个文件名。"]
  },
  configAllowMcp: {
    title: "允许 MCP 使用",
    subtitle: "允许 AI/Agent 通过 MCP 发起这个配置的任务。",
    body: "开启后，授权过的 MCP 客户端才可以看到并使用这份配置发起打包。不开启时，只能网页使用。",
    tips: ["涉及自动化入口，建议只给稳定配置开启。", "MCP 是否能正式打包，还受后端 Agent 权限控制。"]
  }
};

async function api(path, options = {}) {
  return AppRuntime.requestJson(path, options);
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
  return AppRuntime.requestText(path);
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
  AppRuntime.setButtonBusy(id, busy, busyText);
}

async function init() {
  bindEvents();
  installConfigFieldHelp();
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
    startDashboardEvents();
  } catch {
    showLogin();
  }
}

function bindEvents() {
  $("loginForm").addEventListener("submit", login);
  $("logoutBtn").addEventListener("click", logout);
  $("refreshBtn").addEventListener("click", refreshAll);
  $("projectForm").addEventListener("submit", createProject);
  $("configForm").addEventListener("submit", createConfig);
  $("buildForm").addEventListener("submit", startBuild);
  $("userForm").addEventListener("submit", saveUser);
  $("passwordForm").addEventListener("submit", changeMyPassword);
  $("userCancelBtn").addEventListener("click", resetUserForm);
  $("usersList").addEventListener("click", handleUsersListClick);
  $("projectsList").addEventListener("click", handleProjectsListClick);
  $("jobsList").addEventListener("click", handleJobsListClick);
  $("configCancelEditBtn").addEventListener("click", resetConfigForm);
  $("configDeleteBtn").addEventListener("click", () => {
    if (state.editingConfigId) deleteConfig(state.editingConfigId);
  });
  $("jobModalClose").addEventListener("click", closeJobModal);
  $("jobModal").addEventListener("click", (event) => {
    if (event.target === $("jobModal")) closeJobModal();
  });
  $("fieldHelpClose").addEventListener("click", closeFieldHelp);
  $("fieldHelpModal").addEventListener("click", (event) => {
    if (event.target === $("fieldHelpModal")) closeFieldHelp();
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isFieldHelpOpen()) closeFieldHelp();
    if (event.key === "Escape" && isJobModalOpen()) closeJobModal();
  });
  $("configCreateFile").addEventListener("change", toggleConfigFileFields);
  $("configAppStoreConnectUploadEnabled").addEventListener("change", toggleUploadSections);
  $("configGooglePlayUploadEnabled").addEventListener("change", toggleUploadSections);
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

function installConfigFieldHelp() {
  Object.entries(CONFIG_FIELD_HELP).forEach(([controlId, help]) => {
    const control = $(controlId);
    const label = control?.closest("label");
    if (!control || !label || label.querySelector(".field-help-button")) {
      return;
    }

    const textNode = Array.from(label.childNodes)
      .find((node) => node.nodeType === Node.TEXT_NODE && node.textContent?.trim());
    const labelText = document.createElement("span");
    labelText.className = "label-text";
    labelText.textContent = textNode?.textContent?.trim() || help.title;

    const button = document.createElement("button");
    button.type = "button";
    button.className = "field-help-button";
    button.textContent = "?";
    button.setAttribute("aria-label", `查看 ${help.title} 说明`);
    button.addEventListener("mousedown", (event) => event.preventDefault());
    button.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      openFieldHelp(controlId);
    });

    labelText.appendChild(button);
    if (textNode) {
      label.replaceChild(labelText, textNode);
    } else {
      label.insertBefore(labelText, control);
    }
  });
}

function openFieldHelp(controlId) {
  const help = CONFIG_FIELD_HELP[controlId];
  if (!help) return;

  $("fieldHelpTitle").textContent = help.title;
  $("fieldHelpSubTitle").textContent = help.subtitle || "";
  $("fieldHelpBody").innerHTML = [
    `<p>${escapeHtml(help.body)}</p>`,
    help.tips?.length
      ? `<section><h3>怎么填</h3><ul>${help.tips.map((tip) => `<li>${formatHelpText(tip)}</li>`).join("")}</ul></section>`
      : ""
  ].join("");
  $("fieldHelpModal").classList.remove("hidden");
  $("fieldHelpModal").setAttribute("aria-hidden", "false");
}

function closeFieldHelp() {
  $("fieldHelpModal").classList.add("hidden");
  $("fieldHelpModal").setAttribute("aria-hidden", "true");
}

function isFieldHelpOpen() {
  return !$("fieldHelpModal").classList.contains("hidden");
}

function formatHelpText(value) {
  return escapeHtml(value).replace(/`([^`]+)`/g, "<code>$1</code>");
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
    startDashboardEvents();
  } catch (error) {
    showLoginError(error);
  } finally {
    setButtonBusy("loginBtn", false);
  }
}

async function logout() {
  await api("/api/auth/logout", { method: "POST" });
  state.user = null;
  stopDashboardEvents();
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
  renderPermissionChrome();
}

function applyDashboard(dashboard) {
  state.projects = dashboard.projects || [];
  state.configs = dashboard.configs || [];
  state.jobs = dashboard.jobs || [];
  state.settings = dashboard.settings || null;
  renderProjects();
  renderConfigsSelects();
  renderJobs();
  renderMetrics();
  renderWorkers(dashboard.workers || []);
  updatePermissionControls();
}

function startDashboardEvents() {
  stopDashboardEvents();
  state.events = AppRuntime.connectEvents({
    onDashboard: (dashboard) => applyDashboard(dashboard),
    onStatus: (message) => {
      if (state.user && state.activeTab === "builds") showMessage(message);
    },
    onFallbackPoll: () => refreshJobsSoft(),
    fallbackIntervalMs: 5000,
  });
}

function stopDashboardEvents() {
  if (state.events) {
    state.events.close();
    state.events = null;
  }
}

function setTab(tab) {
  state.activeTab = tab;
  document.querySelectorAll("aside button[data-tab]").forEach((button) => {
    button.classList.toggle("active", button.dataset.tab === tab);
  });
  document.querySelectorAll(".tab").forEach((panel) => panel.classList.add("hidden"));
  $(`${tab}Tab`).classList.remove("hidden");
  $("pageTitle").textContent = { builds: "打包任务", projects: "项目配置", workers: "Worker", audit: "审计日志", users: "用户权限", help: "填写说明" }[tab];
  if (tab === "users" && isAdmin()) {
    refreshUsers();
  }
}

async function refreshAll(options = {}) {
  const showSuccess = options.showSuccess ?? true;
  const throwOnError = options.throwOnError ?? false;
  clearMessage();
  setButtonBusy("refreshBtn", true, "刷新中...");
  try {
    applyDashboard(await api("/api/dashboard"));
    if (state.activeTab === "audit") await refreshAudit();
    if (state.activeTab === "users" && isAdmin()) await refreshUsers();
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
  if (!state.user) return;
  try {
    applyDashboard(await api("/api/dashboard"));
    if (state.selectedJobId && isJobModalOpen()) await refreshJobModal(state.selectedJobId);
  } catch {
    // 登录过期时下一次手动刷新会提示。
  }
}

function isAdmin() {
  return state.user?.role === "Admin";
}

function canManageProjects() {
  return state.user?.role === "Admin" || state.user?.role === "ProjectOwner";
}

function canStartBuild() {
  return ["Admin", "ProjectOwner", "Builder", "Agent"].includes(state.user?.role || "");
}

function renderPermissionChrome() {
  $("usersNav").classList.remove("hidden");
  $("adminUsersPanel").classList.toggle("hidden", !isAdmin());
  $("usersPermissionHint").classList.toggle("hidden", isAdmin());
  updatePermissionControls();
}

function updatePermissionControls() {
  const manageReason = canManageProjects() ? "" : "当前角色只能查看，不能维护项目或配置。";
  const buildReason = canStartBuild() ? "" : "当前角色不能发起构建任务。";

  setFormDisabled("projectForm", Boolean(manageReason), manageReason);
  setFormDisabled("configForm", Boolean(manageReason), manageReason);
  setFormDisabled("buildForm", Boolean(buildReason), buildReason);
}

function setFormDisabled(formId, disabled, reason) {
  const form = $(formId);
  if (!form) return;
  form.querySelectorAll("input, select, textarea, button").forEach((control) => {
    if (disabled) {
      if (!control.disabled) {
        control.dataset.permissionDisabled = "true";
        control.disabled = true;
      }
      control.title = reason;
      return;
    }

    if (control.dataset.permissionDisabled === "true") {
      control.disabled = false;
      delete control.dataset.permissionDisabled;
      control.removeAttribute("title");
    }
  });
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
  setButtonBusy("configSaveBtn", true, state.editingConfigId ? "更新中..." : "保存中...");
  try {
    const createFile = $("configCreateFile").checked;
    if (state.editingConfigId && createFile) {
      await api(`/api/config-files/${encodeURIComponent(state.editingConfigId)}`, {
        method: "PUT",
        body: JSON.stringify(collectConfigFilePayload()),
      });
    } else if (state.editingConfigId) {
      await api(`/api/configs/${encodeURIComponent(state.editingConfigId)}`, {
        method: "PUT",
        body: JSON.stringify(collectConfigRecordPayload()),
      });
    } else if (createFile) {
      await api("/api/config-files", {
        method: "POST",
        body: JSON.stringify(collectConfigFilePayload()),
      });
    } else {
      await api("/api/configs", {
        method: "POST",
        body: JSON.stringify(collectConfigRecordPayload()),
      });
    }
    const wasEditing = Boolean(state.editingConfigId);
    resetConfigForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage(wasEditing ? "配置已更新。" : "配置已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("configSaveBtn", false);
  }
}

function collectConfigRecordPayload() {
  return {
    projectId: $("configProject").value,
    name: $("configName").value,
    buildPlatform: $("configBuildPlatform").value,
    configPath: $("configPath").value,
    allowMcpBuild: $("configAllowMcp").checked,
  };
}

function collectConfigFilePayload() {
  return {
    ...collectConfigRecordPayload(),
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
    appStoreConnectUploadEnabled: $("configAppStoreConnectUploadEnabled").checked,
    appStoreConnectApiKeyPath: $("configAppStoreConnectApiKeyPath").value || null,
    appStoreConnectApiKeyId: $("configAppStoreConnectApiKeyId").value || null,
    appStoreConnectApiIssuerId: $("configAppStoreConnectApiIssuerId").value || null,
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
  };
}

function toggleConfigFileFields() {
  const createFile = $("configCreateFile").checked;
  if (createFile && !state.editingConfigId) {
    state.manualConfigPath = $("configPath").value;
  }

  $("configFileFields").classList.toggle("hidden", !createFile);
  $("configPath").disabled = createFile;
  $("configPath").required = !createFile;
  if (createFile) {
    if (state.editingConfigId) {
      $("configPath").value = state.editingConfigPath;
    } else {
      fillConfigFileDefaults();
    }
    updateConfigPathPreview();
  } else {
    $("configPath").value = state.editingConfigId ? state.editingConfigPath : state.manualConfigPath;
  }
}

function fillConfigFileDefaults(options = {}) {
  const project = state.projects.find((item) => item.id === $("configProject").value);
  const configName = $("configName").value.trim() || "release";
  const platform = $("configBuildPlatform").value || project?.defaultBuildPlatform || "ios";
  if (!$("configBuildPlatform").value && project?.defaultBuildPlatform) {
    $("configBuildPlatform").value = project.defaultBuildPlatform;
  }

  if (!state.editingConfigId && (options.forceFileName || !$("configFileName").value.trim())) {
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
  if (state.editingConfigId) {
    $("configPath").value = state.editingConfigPath;
    return;
  }

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
  $("configAppStoreConnectUploadEnabled").checked = false;
  $("configAndroidBuildFormat").value = "aab";
  $("configGooglePlayTrack").value = "internal";
  $("configGooglePlayReleaseStatus").value = "draft";
  $("configGooglePlayUploadArtifact").value = "aab";
  $("configGooglePlayUploadEnabled").checked = false;
  $("configGooglePlayChangesNotSentForReview").checked = false;
  $("configOverwriteFile").checked = false;
  toggleUploadSections();
}

function resetConfigForm() {
  state.editingConfigId = null;
  state.editingConfigPath = "";
  state.manualConfigPath = "";
  $("configForm").reset();
  $("configFormTitle").textContent = "新增配置";
  $("configSaveBtn").textContent = "保存配置";
  $("configSaveBtn").dataset.defaultText = "保存配置";
  $("configCancelEditBtn").classList.add("hidden");
  $("configDeleteBtn").classList.add("hidden");
  setConfigFileDefaults();
  toggleConfigFileFields();
  togglePlatformFields();
  toggleUploadSections();
  $("configAllowMcp").checked = false;
}

function togglePlatformFields() {
  const platform = $("configBuildPlatform").value || "ios";
  $("iosConfigFields").classList.toggle("hidden", platform !== "ios");
  $("androidConfigFields").classList.toggle("hidden", platform !== "android");
  toggleUploadSections();
}

function toggleUploadSections() {
  const appStoreUploadEnabled = $("configAppStoreConnectUploadEnabled")?.checked;
  const googlePlayUploadEnabled = $("configGooglePlayUploadEnabled")?.checked;

  $("appStoreConnectAdvancedFields")?.classList.toggle("hidden", !appStoreUploadEnabled);
  $("googlePlayAdvancedFields")?.classList.toggle("hidden", !googlePlayUploadEnabled);

  if (appStoreUploadEnabled) {
    $("appStoreConnectSection")?.setAttribute("open", "");
  }

  if (googlePlayUploadEnabled) {
    $("googlePlaySection")?.setAttribute("open", "");
  }
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

function fileNameFromPath(value) {
  return String(value || "")
    .split(/[\\/]/)
    .filter(Boolean)
    .pop() || "";
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
        clientRequestId: AppRuntime.createRequestId("build"),
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

async function handleProjectsListClick(event) {
  const editButton = event.target.closest("[data-edit-config-id]");
  if (editButton) {
    await editConfig(editButton.dataset.editConfigId);
    return;
  }

  const deleteButton = event.target.closest("[data-delete-config-id]");
  if (deleteButton) {
    await deleteConfig(deleteButton.dataset.deleteConfigId);
  }
}

async function editConfig(configId) {
  clearMessage();
  const config = state.configs.find((item) => item.id === configId);
  if (!config) {
    showError(new Error("配置不存在，可能已经被删除。"));
    return;
  }

  state.editingConfigId = config.id;
  state.editingConfigPath = config.configPath;
  $("configFormTitle").textContent = "编辑配置";
  $("configSaveBtn").textContent = "更新配置";
  $("configSaveBtn").dataset.defaultText = "更新配置";
  $("configCancelEditBtn").classList.remove("hidden");
  $("configDeleteBtn").classList.remove("hidden");

  $("configProject").value = config.projectId;
  $("configName").value = config.name || "";
  $("configBuildPlatform").value = config.buildPlatform || "ios";
  $("configPath").value = config.configPath || "";
  $("configAllowMcp").checked = Boolean(config.allowMcpBuild);
  $("configCreateFile").checked = false;
  toggleConfigFileFields();
  togglePlatformFields();

  try {
    const file = await api(`/api/configs/${encodeURIComponent(config.id)}/file`);
    fillConfigFormFromJson(file.content || {}, config);
    $("configCreateFile").checked = true;
    toggleConfigFileFields();
    showMessage("配置已载入，可以直接修改后保存。");
  } catch (error) {
    showError(error);
  }

  $("configForm").scrollIntoView({ behavior: "smooth", block: "start" });
}

function fillConfigFormFromJson(content, config) {
  $("configProject").value = config.projectId;
  $("configName").value = content.configName || config.name || "";
  $("configBuildPlatform").value = content.buildPlatform || config.buildPlatform || "ios";
  $("configPath").value = config.configPath || "";
  $("configFileName").value = fileNameFromPath(config.configPath);
  $("configProjectDirectoryName").value = content.projectDirectoryName || "";
  $("configUnityRelativePath").value = content.unityProjectRelativePath || ".";
  $("configUnityVersion").value = content.unityVersion || "";
  $("configUnityExecutablePath").value = content.unityExecutablePath || "";
  $("configProductName").value = content.productName || "";
  $("configBundleIdentifier").value = content.bundleIdentifier || "";
  $("configBuildNumber").value = content.buildNumber || "1";
  $("configBundleVersion").value = content.bundleVersion || "1.0.0";
  $("configSyncUnityVersion").checked = content.syncBundleVersionFromUnity !== false;
  $("configAutoIncrementBuild").checked = content.autoIncrementBuildNumber !== false;
  $("configTeamId").value = content.teamId || "";
  $("configIosDeploymentTarget").value = content.iosDeploymentTarget || "13.0";
  $("configExportMethod").value = content.exportMethod || "development";
  $("configSigningStyle").value = content.signingStyle || "automatic";
  $("configAllowProvisioningUpdates").checked = content.allowProvisioningUpdates !== false;
  $("configCopyArchiveToOrganizer").checked = content.copyArchiveToOrganizer !== false;
  $("configAppStoreConnectUploadEnabled").checked = Boolean(content.appStoreConnectUploadEnabled);
  $("configAppStoreConnectApiKeyPath").value = content.appStoreConnectApiKeyPath || "";
  $("configAppStoreConnectApiKeyId").value = content.appStoreConnectApiKeyId || "";
  $("configAppStoreConnectApiIssuerId").value = content.appStoreConnectApiIssuerId || "";
  $("configAndroidBuildFormat").value = content.androidBuildFormat || "aab";
  $("configAndroidOutputDirectory").value = content.androidOutputDirectory || "";
  $("configApkOutputPath").value = content.apkOutputPath || "";
  $("configAabOutputPath").value = content.aabOutputPath || "";
  $("configAndroidMinSdkVersion").value = content.androidMinSdkVersion || "";
  $("configAndroidTargetSdkVersion").value = content.androidTargetSdkVersion || "";
  $("configAndroidKeystoreName").value = content.androidKeystoreName || "";
  $("configAndroidKeystorePass").value = content.androidKeystorePass || "";
  $("configAndroidKeyaliasName").value = content.androidKeyaliasName || "";
  $("configAndroidKeyaliasPass").value = content.androidKeyaliasPass || "";
  $("configGooglePlayUploadEnabled").checked = Boolean(content.googlePlayUploadEnabled);
  $("configGooglePlayPackageName").value = content.googlePlayPackageName || "";
  $("configGooglePlayServiceAccountJsonPath").value = content.googlePlayServiceAccountJsonPath || "";
  $("configGooglePlayTrack").value = content.googlePlayTrack || "internal";
  $("configGooglePlayReleaseStatus").value = content.googlePlayReleaseStatus || "draft";
  $("configGooglePlayReleaseName").value = content.googlePlayReleaseName || "";
  $("configGooglePlayUploadArtifact").value = content.googlePlayUploadArtifact || "aab";
  $("configGooglePlayChangesNotSentForReview").checked = Boolean(content.googlePlayChangesNotSentForReview);
  $("configGooglePlayUserFraction").value = content.googlePlayUserFraction ?? "";
  $("configOverwriteFile").checked = true;
  $("configAllowMcp").checked = Boolean(config.allowMcpBuild);
  togglePlatformFields();
}

async function deleteConfig(configId) {
  const config = state.configs.find((item) => item.id === configId);
  if (!config) {
    showError(new Error("配置不存在，可能已经被删除。"));
    return;
  }

  if (!confirm(`确定删除配置「${config.name}」吗？\n\n删除后网页列表和打包选择里不会再出现它。`)) {
    return;
  }

  const deleteFile = confirm(`是否同时删除这个 JSON 配置文件？\n\n${config.configPath}\n\n确定 = 删除网页记录和 JSON 文件\n取消 = 只删除网页记录，JSON 文件保留`);
  try {
    await api(`/api/configs/${encodeURIComponent(config.id)}?deleteFile=${deleteFile ? "true" : "false"}`, {
      method: "DELETE",
    });
    if (state.editingConfigId === config.id) {
      resetConfigForm();
    }
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage(deleteFile ? "配置和 JSON 文件已删除。" : "配置已从网页列表删除，JSON 文件已保留。");
  } catch (error) {
    showError(error);
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
      <div class="config-list">${configs.length ? configs.map(renderConfigRow).join("") : `<div class="muted">暂无配置</div>`}</div>
    </article>`;
  }).join("");
}

function renderConfigRow(config) {
  return `<div class="config-row">
    <div>
      <strong>${escapeHtml(config.name)}</strong> ${platformBadge(config.buildPlatform || "ios")}
      <div class="muted">${escapeHtml(config.configPath)}</div>
      <div class="muted">MCP: ${config.allowMcpBuild ? "允许" : "不允许"} / ${config.enabled ? "启用" : "禁用"}</div>
    </div>
    <div class="item-actions">
      <button class="secondary" type="button" data-edit-config-id="${escapeHtml(config.id)}">编辑</button>
      <button class="danger" type="button" data-delete-config-id="${escapeHtml(config.id)}">删除</button>
    </div>
  </div>`;
}

function renderConfigsSelects() {
  const selectedBuildProject = $("buildProject").value;
  const selectedBuildConfig = $("buildConfig").value;
  const selectedConfigProject = $("configProject").value;
  const projectOptions = state.projects.map((project) => `<option value="${escapeHtml(project.id)}">${escapeHtml(project.name)}</option>`).join("");
  $("buildProject").innerHTML = projectOptions;
  $("configProject").innerHTML = projectOptions;
  if (state.projects.some((project) => project.id === selectedBuildProject)) {
    $("buildProject").value = selectedBuildProject;
  }
  if (state.projects.some((project) => project.id === selectedConfigProject)) {
    $("configProject").value = selectedConfigProject;
  }
  if (!state.editingConfigId) {
    const project = state.projects.find((item) => item.id === $("configProject").value);
    if (project?.defaultBuildPlatform) {
      $("configBuildPlatform").value = project.defaultBuildPlatform;
    }
    fillConfigFileDefaults();
  }
  renderBuildConfigs();
  if (state.configs.some((config) => config.id === selectedBuildConfig && config.projectId === $("buildProject").value)) {
    $("buildConfig").value = selectedBuildConfig;
  }
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

async function refreshUsers() {
  if (!isAdmin()) return;
  state.users = await api("/api/users");
  renderUsers();
}

function renderUsers() {
  if (!isAdmin()) return;
  if (state.users.length === 0) {
    $("usersList").innerHTML = `<article class="item muted">暂无用户。</article>`;
    return;
  }

  $("usersList").innerHTML = state.users.map((user) => `<article class="item">
    <header>
      <div>
        <strong>${escapeHtml(user.displayName || user.userName)}</strong>
        <div class="muted">${escapeHtml(user.userName)} / ${escapeHtml(user.role)}</div>
      </div>
      <span class="status ${user.enabled ? "Succeeded" : "Canceled"}">${user.enabled ? "Enabled" : "Disabled"}</span>
    </header>
    <div class="muted">Created: ${new Date(user.createdAt).toLocaleString()}</div>
    <div class="item-actions">
      <button class="secondary" type="button" data-edit-user-id="${escapeHtml(user.id)}">编辑</button>
      <button class="danger" type="button" data-disable-user-id="${escapeHtml(user.id)}" ${user.enabled ? "" : "disabled"}>禁用</button>
    </div>
  </article>`).join("");
}

async function saveUser(event) {
  event.preventDefault();
  clearMessage();
  const userId = $("userId").value;
  const path = userId ? `/api/users/${encodeURIComponent(userId)}` : "/api/users";
  const method = userId ? "PUT" : "POST";
  setButtonBusy("userSaveBtn", true, userId ? "更新中..." : "保存中...");
  try {
    await api(path, {
      method,
      body: JSON.stringify({
        userName: $("userName").value,
        displayName: $("userDisplayName").value,
        role: $("userRole").value,
        password: $("userPassword").value || null,
        enabled: $("userEnabled").checked,
      }),
    });
    resetUserForm();
    await refreshUsers();
    showMessage(userId ? "用户已更新。" : "用户已创建。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("userSaveBtn", false);
  }
}

function handleUsersListClick(event) {
  const editButton = event.target.closest("[data-edit-user-id]");
  if (editButton) {
    fillUserForm(editButton.dataset.editUserId);
    return;
  }

  const disableButton = event.target.closest("[data-disable-user-id]");
  if (disableButton) {
    AppRuntime.runAction(disableButton, () => disableUser(disableButton.dataset.disableUserId), {
      busyText: "禁用中...",
      onError: showError,
    });
  }
}

async function disableUser(userId) {
  await api(`/api/users/${encodeURIComponent(userId)}`, { method: "DELETE" });
  if ($("userId").value === userId) {
    resetUserForm();
  }
  await refreshUsers();
  showMessage("用户已禁用。");
}

function fillUserForm(userId) {
  const user = state.users.find((item) => item.id === userId);
  if (!user) return;
  $("userId").value = user.id;
  $("userName").value = user.userName;
  $("userDisplayName").value = user.displayName || "";
  $("userRole").value = user.role;
  $("userPassword").value = "";
  $("userEnabled").checked = Boolean(user.enabled);
  $("userFormTitle").textContent = "编辑用户";
  $("userSaveBtn").textContent = "更新用户";
  $("userSaveBtn").dataset.defaultText = "更新用户";
  showMessage("用户已载入。密码留空表示不修改。");
}

function resetUserForm() {
  $("userForm").reset();
  $("userId").value = "";
  $("userRole").value = "Builder";
  $("userEnabled").checked = true;
  $("userFormTitle").textContent = "新增用户";
  $("userSaveBtn").textContent = "保存用户";
  $("userSaveBtn").dataset.defaultText = "保存用户";
}

async function changeMyPassword(event) {
  event.preventDefault();
  clearMessage();
  setButtonBusy("passwordSaveBtn", true, "更新中...");
  try {
    await api("/api/me/password", {
      method: "POST",
      body: JSON.stringify({
        currentPassword: $("currentPassword").value,
        newPassword: $("newPassword").value,
      }),
    });
    state.user = null;
    stopDashboardEvents();
    showLogin();
    showLoginError(new Error("密码已更新，请使用新密码重新登录。"));
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("passwordSaveBtn", false);
    $("passwordForm").reset();
  }
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
