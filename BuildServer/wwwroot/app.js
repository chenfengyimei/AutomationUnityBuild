const state = {
  user: null,
  projects: [],
  configs: [],
  jobs: [],
  users: [],
  settings: null,
  emailSettings: null,
  notificationContacts: [],
  projectProfiles: [],
  certificateProfiles: [],
  signingProfiles: [],
  unityProjectProfiles: [],
  storageOverview: null,
  storageJobs: [],
  selectedStorageJobIds: new Set(),
  manualConfigPath: "",
  configFileNameManuallyEdited: false,
  selectedJobId: null,
  pendingConfigDeleteId: null,
  editingConfigId: null,
  editingConfigPath: "",
  selectedUserId: "",
  activeTab: "builds",
  events: null,
  jobModalTimer: null,
  gatewayAgentTimer: null,
};

const $ = (id) => document.getElementById(id);

const STATUS_LABELS = {
  Queued: "排队中",
  Running: "执行中",
  Succeeded: "成功",
  Failed: "失败",
  Canceled: "已取消",
  Cancelled: "已取消",
  Online: "在线",
  Offline: "离线",
  Disabled: "已停用",
  Enabled: "已启用",
  Unknown: "未知",
  Idle: "空闲",
  Busy: "忙碌",
};

const ROLE_LABELS = {
  Admin: "管理员",
  ProjectOwner: "项目负责人",
  Builder: "构建员",
  Viewer: "查看者",
  Agent: "自动化账号",
};

const PLATFORM_LABELS = {
  ios: "iOS",
  android: "Android",
  tiktok: "TikTok",
  auto: "自动",
};

const SOURCE_LABELS = {
  Web: "网页提交",
  Manual: "手动提交",
  Gateway: "Gateway 转发",
  LinuxGateway: "LinuxGateway 转发",
  Mcp: "MCP 调用",
  MCP: "MCP 调用",
  Agent: "自动化提交",
};

const ARTIFACT_TYPE_LABELS = {
  ipa: "IPA 包",
  apk: "APK 包",
  aab: "AAB 包",
  archive: "归档",
  log: "日志",
  logs: "日志",
  folder: "文件夹",
  directory: "目录",
  file: "文件",
};

const AUDIT_ACTION_LABELS = {
  "auth.login": "用户登录",
  "auth.logout": "用户退出",
  "user.create": "新增用户",
  "user.update": "更新用户",
  "user.disable": "停用用户",
  "user.delete": "删除用户",
  "user.password": "修改密码",
  "project.create": "新增项目",
  "project.update": "更新项目",
  "project.delete": "删除项目",
  "config.create": "新增配置",
  "config.update": "更新配置",
  "config.delete": "删除配置",
  "build.create": "提交构建",
  "build.cancel": "取消构建",
  "gateway.connect": "连接 Gateway",
  "gateway.disconnect": "断开 Gateway",
};

const AUDIT_TARGET_LABELS = {
  User: "用户",
  Project: "项目",
  Config: "配置",
  Build: "构建任务",
  Job: "任务",
  Worker: "Worker",
  Gateway: "Gateway",
  System: "系统",
};

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
    tips: ["路径是运行 BuildServer 的机器上的路径，不是你浏览器电脑的路径。", "切换到「创建新配置」模式可以让网页自动生成 JSON。"]
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
    tips: ["例如仓库是 YourGame.git，这里一般就是 YourGame。", "不要填完整路径，只填一个文件夹名。"]
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
    body: "iOS 和 Android 都需要它。一般格式是 com.company.game，比如 com.yourcompany.yourgame。",
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
    tips: ["演练模式（dry-run）不会真的递增。", "Build Number 必须是纯数字才能自动 +1。"]
  },
  configTeamId: {
    title: "Apple Team ID",
    subtitle: "Apple Developer 团队 ID。",
    body: "这是 10 位字母数字，不是公司名。Xcode 签名时需要它来知道用哪个 Apple 开发者团队。",
    tips: ["可以在 Apple Developer Membership 或 Xcode Accounts 里查看。", "例如 ABCDE12345，不要填 Your Company Ltd. 这种公司名。"]
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
    tips: ["新手和专用打包机建议先用自动签名。", "手动签名通常要配 provisioningProfiles 等高级字段。"]
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
    const loginError = $("loginError");
    if (loginError && type === "error") {
      loginError.textContent = message;
      loginError.classList.remove("hidden");
    } else {
      console[type === "error" ? "error" : "info"](message);
    }
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
    startGatewayAgentPolling();
  } catch {
    showLogin();
  }
}

function bindEvents() {
  $("loginForm").addEventListener("submit", login);
  $("logoutBtn").addEventListener("click", logout);
  $("refreshBtn").addEventListener("click", refreshAll);
  $("configForm").addEventListener("submit", createConfig);
  $("buildForm").addEventListener("submit", startBuild);
  $("userForm").addEventListener("submit", saveUser);
  $("passwordForm").addEventListener("submit", changeMyPassword);
  $("userAddBtn").addEventListener("click", () => openUserModal());
  $("userCancelBtn").addEventListener("click", closeUserModal);
  $("userModalClose").addEventListener("click", closeUserModal);
  $("userModal").addEventListener("click", (event) => {
    if (event.target === $("userModal")) closeUserModal();
  });
  $("usersList").addEventListener("click", handleUsersListClick);
  $("usersList").addEventListener("keydown", handleUsersKeydown);
  $("userDetailPanel").addEventListener("click", handleUsersListClick);
  $("projectsList").addEventListener("click", handleProjectsListClick);
  $("jobsList").addEventListener("click", handleJobsListClick);
  $("configCancelEditBtn").addEventListener("click", resetConfigForm);
  $("configDeleteBtn").addEventListener("click", () => {
    if (state.editingConfigId) deleteConfig(state.editingConfigId);
  });
  $("configDeleteClose").addEventListener("click", closeConfigDeleteModal);
  $("configDeleteCancel").addEventListener("click", closeConfigDeleteModal);
  $("configDeleteConfirmBtn").addEventListener("click", () => {
    AppRuntime.runAction("configDeleteConfirmBtn", confirmDeleteConfig, {
      busyText: "删除中...",
      onError: showError,
    });
  });
  $("configDeleteModal").addEventListener("click", (event) => {
    if (event.target === $("configDeleteModal")) closeConfigDeleteModal();
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
    if (event.key === "Escape" && isUserModalOpen()) closeUserModal();
    if (event.key === "Escape" && isFieldHelpOpen()) closeFieldHelp();
    if (event.key === "Escape" && isConfigDeleteModalOpen()) closeConfigDeleteModal();
    if (event.key === "Escape" && isJobModalOpen()) closeJobModal();
    if (event.key === "Escape" && !$("configFileBrowserModal").classList.contains("hidden")) closeConfigFileBrowser();
  });
  document.querySelectorAll('input[name="configMode"]').forEach((radio) => {
    radio.addEventListener("change", toggleConfigFileFields);
  });
  $("configAppStoreConnectUploadEnabled").addEventListener("change", toggleUploadSections);
  $("configGooglePlayUploadEnabled").addEventListener("change", toggleUploadSections);
  $("configBuildPlatform").addEventListener("change", () => {
    fillConfigFileDefaults({ forceFileName: true });
    togglePlatformFields();
  });
  $("configProject").addEventListener("change", () => {
    const projectId = $("configProject").value;
    const project = state.projects.find((item) => item.id === projectId);
    if (project?.defaultBuildPlatform) {
      $("configBuildPlatform").value = project.defaultBuildPlatform;
    }
    autoFillProjectFields(projectId);
    fillConfigFileDefaults({ forceFileName: true });
  });
  $("configName").addEventListener("input", () => {
    if (!state.configFileNameManuallyEdited && !state.editingConfigId) {
      const configName = $("configName").value.trim() || "release";
      const platform = $("configBuildPlatform").value || "ios";
      $("configFileName").value = `build-${platform}.${safeFilePart(configName)}.json`;
      updateConfigPathPreview();
    }
    fillConfigFileDefaults();
  });
  $("configFileName").addEventListener("input", () => {
    state.configFileNameManuallyEdited = true;
    updateConfigPathPreview();
  });
  $("configPath").addEventListener("input", () => {
    if (!$("configModeCreate").checked) {
      state.manualConfigPath = $("configPath").value;
    }
  });
  document.querySelectorAll("aside button[data-tab]").forEach((button) => {
    button.addEventListener("click", () => setTab(button.dataset.tab));
  });
  $("buildProject").addEventListener("change", renderBuildConfigs);
  $("gatewayConnectForm").addEventListener("submit", connectGateway);
  $("gwDisconnectBtn").addEventListener("click", disconnectGateway);
  $("gwTestBtn").addEventListener("click", () => refreshGatewayAgentStatus().catch(showError));
  $("emailSettingsForm").addEventListener("submit", saveEmailSettings);
  $("emailTestBtn").addEventListener("click", sendTestEmail);
  $("contactForm").addEventListener("submit", saveNotificationContact);
  $("contactCancelBtn").addEventListener("click", resetContactForm);

  $("quickFillUnity").addEventListener("change", () => applyTemplateFill("unity"));
  $("quickFillSigning").addEventListener("change", () => applyTemplateFill("signing"));
  $("quickFillCert").addEventListener("change", () => applyTemplateFill("cert"));
  $("projectProfileForm").addEventListener("submit", saveProjectProfile);
  $("projectProfileCancelBtn").addEventListener("click", resetProjectProfileForm);
  $("projectProfilesList").addEventListener("click", handleProjectProfilesListClick);
  $("certProfileForm").addEventListener("submit", saveCertProfile);
  $("certProfileCancelBtn").addEventListener("click", resetCertProfileForm);
  $("certProfilesList").addEventListener("click", handleCertProfilesListClick);
  $("certProfilePlatform").addEventListener("change", toggleCertProfilePlatformFields);
  $("signingProfileForm").addEventListener("submit", saveSigningProfile);
  $("signingProfileCancelBtn").addEventListener("click", resetSigningProfileForm);
  $("signingProfilesList").addEventListener("click", handleSigningProfilesListClick);
  $("signingProfilePlatform").addEventListener("change", toggleSigningProfilePlatformFields);
  $("unityProfileForm").addEventListener("submit", saveUnityProfile);
  $("unityProfileCancelBtn").addEventListener("click", resetUnityProfileForm);
  $("unityProfilesList").addEventListener("click", handleUnityProfilesListClick);
  $("exportAllBtn").addEventListener("click", toggleExportAll);
  $("exportBtn").addEventListener("click", exportData);
  $("importBtn").addEventListener("click", importData);
  $("browseConfigBtn").addEventListener("click", openConfigFileBrowser);
  $("configFileBrowserClose").addEventListener("click", closeConfigFileBrowser);
  $("configFileBrowserModal").addEventListener("click", (event) => {
    if (event.target === $("configFileBrowserModal")) closeConfigFileBrowser();
  });
  $("configFileBrowserList").addEventListener("click", (event) => {
    const item = event.target.closest("[data-config-file-path]");
    if (!item) return;
    $("configPath").value = item.dataset.configFilePath;
    closeConfigFileBrowser();
    showMessage("已选择配置文件。");
  });



















































































  $("contactsList").addEventListener("click", handleContactsListClick);
  $("storageRefreshBtn").addEventListener("click", () => loadStorageData().catch(showError));
  $("storageStatusFilter").addEventListener("change", () => loadStorageJobs().catch(showError));
  $("storageSelectAllBtn").addEventListener("click", toggleSelectAllStorageJobs);
  




























































































































































































































































































































































































































  $("storageDeleteBtn").addEventListener("click", batchDeleteStorage);
  $("storageJobsList").addEventListener("click", (event) => {
    if (!event.target.closest) return;
    const deleteBtn = event.target.closest("[data-delete-storage-id]");
    if (deleteBtn) {
      deleteSingleStorageJob(deleteBtn.dataset.deleteStorageId);
    }
  });
  $("storageJobsList").addEventListener("change", (event) => {
    const checkbox = event.target.closest(".storage-job-checkbox");
    if (!checkbox) return;
    const jobId = checkbox.dataset.jobId;
    if (checkbox.checked) {
      state.selectedStorageJobIds.add(jobId);
    } else {
      state.selectedStorageJobIds.delete(jobId);
    }
  });
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
  closeConfigDeleteModal();
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
  $("userInfo").textContent = `${state.user.displayName || state.user.userName} / ${roleLabel(state.user.role)}`;
  setEventStatus("实时连接准备中", "warn");
  renderPermissionChrome();
}

function applyDashboard(dashboard) {
  state.projects = dashboard.projects || [];
  state.configs = dashboard.configs || [];
  state.jobs = dashboard.jobs || [];
  state.settings = dashboard.settings || null;
  state.notificationContacts = dashboard.notificationContacts || [];
  state.projectProfiles = dashboard.projectProfiles || [];
  state.certificateProfiles = dashboard.certificateProfiles || [];
  state.signingProfiles = dashboard.signingProfiles || [];
  state.unityProjectProfiles = dashboard.unityProjectProfiles || [];
  renderProjects();
  renderConfigsSelects();
  renderJobs();
  renderMetrics();
  renderWorkers(dashboard.workers || []);
  renderContacts();
  renderQuickFillDropdowns();
  renderProjectProfiles();
  renderCertProfiles();
  renderSigningProfiles();
  renderUnityProfiles();
  updatePermissionControls();
}

function startDashboardEvents() {
  stopDashboardEvents();
  state.events = AppRuntime.connectEvents({
    onDashboard: (dashboard) => {
      applyDashboard(dashboard);
      setEventStatus("实时连接已同步", "ok");
    },
    onStatus: (message) => {
      setEventStatus(message, message.includes("轮询") || message.includes("重连") || message.includes("不可用") ? "warn" : "ok");
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
  setEventStatus("实时连接已关闭", "warn");
}

function setEventStatus(message, tone = "") {
  const element = $("eventStatus");
  if (!element) return;
  element.textContent = message;
  element.classList.toggle("ok", tone === "ok");
  element.classList.toggle("warn", tone === "warn");
}

function setTab(tab) {
  state.activeTab = tab;
  document.querySelectorAll("aside button[data-tab]").forEach((button) => {
    button.classList.toggle("active", button.dataset.tab === tab);
  });
  document.querySelectorAll(".tab").forEach((panel) => panel.classList.add("hidden"));
  $(`${tab}Tab`).classList.remove("hidden");
  const title = { builds: "打包任务", projects: "配置管理", projectProfiles: "项目管理", unityProfiles: "工程管理", certProfiles: "证书管理", signingProfiles: "签名管理", dataManager: "数据管理", workers: "Worker 节点", audit: "审计日志", users: "用户权限", help: "填写说明", settings: "项目设置", storage: "存储管理", gateway: "Gateway 连接" }[tab];
  $("pageTitle").textContent = title;
  $("activeRouteTag").textContent = title;
  if (tab === "users" && isAdmin()) {
    refreshUsers();
  }
  if (tab === "audit") {
    $("auditList").innerHTML = loadingItem("正在读取审计日志...");
    refreshAudit().catch(showError);
  }
  if (tab === "gateway") {
    refreshGatewayAgentStatus().catch(() => {});
  }
  if (tab === "settings" && canManageProjects()) {
    loadEmailSettings().catch(showError);
  }
  if (tab === "storage" && canManageProjects()) {
    loadStorageData().catch(showError);
  }
}

async function refreshAll(options = {}) {
  const showSuccess = options.showSuccess ?? true;
  const throwOnError = options.throwOnError ?? false;
  clearMessage();
  setEventStatus("手动刷新中", "warn");
  setButtonBusy("refreshBtn", true, "刷新中...");
  try {
    applyDashboard(await api("/api/dashboard"));
    if (state.activeTab === "audit") await refreshAudit();
    if (state.activeTab === "users" && isAdmin()) await refreshUsers();
    if (state.selectedJobId && isJobModalOpen()) await refreshJobModal(state.selectedJobId);
    if (showSuccess) {
      showMessage("数据已刷新。");
    }
    setEventStatus("数据已刷新", "ok");
  } catch (error) {
    showError(error);
    setEventStatus("刷新失败，等待重连", "warn");
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
    const createFile = $("configModeCreate").checked;
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
    googlePlayUserFraction: $("configGooglePlayUploadEnabled").checked
      ? parseOptionalNumber($("configGooglePlayUserFraction").value)
      : null,
    overwriteExisting: $("configOverwriteFile").checked,
    tiktokAppId: $("configTiktokAppId").value || null,
    tiktokAccessToken: $("configTiktokAccessToken").value || null,
    tiktokGameName: $("configTiktokGameName").value || null,
    tiktokWebglOutputDirectory: $("configTiktokWebglOutputDirectory").value || null,
    tiktokUploadEnabled: $("configTiktokUploadEnabled").checked,
    tiktokApiEndpoint: $("configTiktokApiEndpoint").value || null,
  };
}

function toggleConfigFileFields() {
  const createFile = $("configModeCreate").checked;
  if (createFile && !state.editingConfigId) {
    state.manualConfigPath = $("configPath").value;
  }

  $("configFileFields").classList.toggle("hidden", !createFile);
  $("configPathLabel").classList.toggle("hidden", createFile);
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

  if (!state.editingConfigId && !state.configFileNameManuallyEdited) {
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
  if (!$("configModeCreate").checked) return;
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
  $("configTiktokUploadEnabled").checked = false;
  $("configTiktokApiEndpoint").value = "https://open-api.tiktokglobalshop.com";
  toggleUploadSections();
}

function resetConfigForm() {
  state.editingConfigId = null;
  state.editingConfigPath = "";
  state.manualConfigPath = "";
  state.configFileNameManuallyEdited = false;
  $("configForm").reset();
  $("configModeCreate").checked = true;
  $("configModeExisting").checked = false;
  $("configFormTitle").textContent = "新增配置";
  $("configSaveBtn").textContent = "保存配置";
  $("configSaveBtn").dataset.defaultText = "保存配置";
  $("configCancelEditBtn").classList.add("hidden");
  $("configDeleteBtn").classList.add("hidden");
  $("quickFillUnity").value = "";
  $("quickFillSigning").value = "";
  $("quickFillCert").value = "";
  $("unitySection")?.classList.remove("hidden");
  $("iosSigningSection")?.classList.remove("hidden");
  $("androidSigningSection")?.classList.remove("hidden");
  $("appStoreConnectSection")?.classList.remove("hidden");
  $("googlePlaySection")?.classList.remove("hidden");
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
  $("tiktokConfigFields").classList.toggle("hidden", platform !== "tiktok");
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
        notifyEmails: ($("buildNotifyEmails").value || "").split(",").map((s) => s.trim()).filter(Boolean),
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
  await api(`/api/builds/${encodeURIComponent(jobId)}/cancel`, { method: "POST" });
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
    if (!canManageProjects()) {
      showError(new Error("当前角色不能编辑配置。"));
      return;
    }
    await editConfig(editButton.dataset.editConfigId);
    return;
  }

  const deleteButton = event.target.closest("[data-delete-config-id]");
  if (deleteButton) {
    if (!canManageProjects()) {
      showError(new Error("当前角色不能删除配置。"));
      return;
    }
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
  $("configModeCreate").checked = false;
  $("configModeExisting").checked = true;
  toggleConfigFileFields();
  togglePlatformFields();

  try {
    const file = await api(`/api/configs/${encodeURIComponent(config.id)}/file`);
    fillConfigFormFromJson(file.content || {}, config);
    $("configModeCreate").checked = true;
    $("configModeExisting").checked = false;
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
  $("configTiktokAppId").value = content.tiktokAppId || "";
  $("configTiktokAccessToken").value = content.tiktokAccessToken || "";
  $("configTiktokGameName").value = content.tiktokGameName || "";
  $("configTiktokWebglOutputDirectory").value = content.tiktokWebglOutputDirectory || "";
  $("configTiktokUploadEnabled").checked = Boolean(content.tiktokUploadEnabled);
  $("configTiktokApiEndpoint").value = content.tiktokApiEndpoint || "https://open-api.tiktokglobalshop.com";
  togglePlatformFields();
}

function deleteConfig(configId) {
  openConfigDeleteModal(configId);
}

function openConfigDeleteModal(configId) {
  const config = state.configs.find((item) => item.id === configId);
  if (!config) {
    showError(new Error("配置不存在，可能已经被删除。"));
    return;
  }

  state.pendingConfigDeleteId = config.id;
  $("configDeleteTitle").textContent = "删除配置";
  $("configDeleteSubTitle").textContent = config.name;
  $("configDeleteBody").innerHTML = [
    `<p>确认删除配置「<strong>${escapeHtml(config.name)}</strong>」吗？</p>`,
    "<p>删除后网页列表和打包选择里不会再出现它。已有任务记录不会被删除。</p>",
  ].join("");
  $("configDeleteFile").checked = false;
  $("configDeleteFilePath").textContent = config.configPath || "未登记 JSON 配置文件路径";
  $("configDeleteModal").classList.remove("hidden");
  $("configDeleteModal").setAttribute("aria-hidden", "false");
}

function closeConfigDeleteModal() {
  const modal = $("configDeleteModal");
  if (!modal) {
    return;
  }

  modal.classList.add("hidden");
  modal.setAttribute("aria-hidden", "true");
  state.pendingConfigDeleteId = null;
}

function isConfigDeleteModalOpen() {
  return !$("configDeleteModal").classList.contains("hidden");
}

async function confirmDeleteConfig() {
  const config = state.configs.find((item) => item.id === state.pendingConfigDeleteId);
  if (!config) {
    closeConfigDeleteModal();
    throw new Error("配置不存在，可能已经被删除。");
  }

  const deleteFile = $("configDeleteFile").checked;
  await api(`/api/configs/${encodeURIComponent(config.id)}?deleteFile=${deleteFile ? "true" : "false"}`, {
    method: "DELETE",
  });
  if (state.editingConfigId === config.id) {
    resetConfigForm();
  }
  closeConfigDeleteModal();
  await refreshAll({ showSuccess: false, throwOnError: true });
  showMessage(deleteFile ? "配置和 JSON 文件已删除。" : "配置已从网页列表删除，JSON 文件已保留。");
}

function renderProjects() {
  if (state.projects.length === 0) {
    $("projectsList").innerHTML = `<div class="empty-state">暂无项目。请先在左侧表单新增项目。</div>`;
    return;
  }

  $("projectsList").innerHTML = `<div class="project-stack">${state.projects.map(renderProjectPanel).join("")}</div>`;
}

function renderProjectPanel(project) {
  const configs = state.configs.filter((config) => config.projectId === project.id);
  return `<article class="project-panel">
    <header class="project-header">
      <div>
        <strong>${escapeHtml(project.name)}</strong>
        <div class="muted small">${escapeHtml(project.repositoryUrl || "-")}</div>
      </div>
      <span class="status ${project.enabled ? "Succeeded" : "Canceled"}">${enabledLabel(project.enabled)}</span>
    </header>
    <dl class="project-meta">
      <div><dt>默认分支</dt><dd>${escapeHtml(project.defaultBranch || "-")}</dd></div>
      <div><dt>默认平台</dt><dd>${platformBadge(project.defaultBuildPlatform || "ios")}</dd></div>
      <div><dt>配置数</dt><dd>${configs.length}</dd></div>
      <div><dt>Workspace</dt><dd>${escapeHtml(project.workspaceRoot || "-")}</dd></div>
      <div><dt>Artifacts</dt><dd>${escapeHtml(project.artifactsRoot || "-")}</dd></div>
    </dl>
    ${configs.length ? `<div class="table-shell project-config-shell">
      <table class="data-table project-config-table">
        <thead>
          <tr>
            <th>配置</th>
            <th>平台</th>
            <th>状态</th>
            <th>MCP</th>
            <th>配置路径</th>
            <th class="table-actions">操作</th>
          </tr>
        </thead>
        <tbody>
          ${configs.map(renderConfigRow).join("")}
        </tbody>
      </table>
    </div>` : `<div class="empty-state compact">暂无配置。使用上方配置表单新增。</div>`}
  </article>`;
}

function renderConfigRow(config) {
  const canManage = canManageProjects();
  const disabled = canManage ? "" : "disabled";
  const title = canManage ? "" : "当前角色不能维护项目或配置。";

  return `<tr>
    <td><strong>${escapeHtml(config.name)}</strong></td>
    <td>${platformBadge(config.buildPlatform || "ios")}</td>
    <td><span class="status ${config.enabled ? "Succeeded" : "Canceled"}">${enabledLabel(config.enabled)}</span></td>
    <td><span class="role-pill">${config.allowMcpBuild ? "允许" : "不允许"}</span></td>
    <td class="path-cell">${escapeHtml(config.configPath || "-")}</td>
    <td class="table-actions">
      <button class="secondary" type="button" data-edit-config-id="${escapeHtml(config.id)}" title="${escapeHtml(title)}" ${disabled}>编辑</button>
      <button class="danger" type="button" data-delete-config-id="${escapeHtml(config.id)}" title="${escapeHtml(title)}" ${disabled}>删除</button>
    </td>
  </tr>`;
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
    const projectChanged = $("configProject").value !== selectedConfigProject;
    if (projectChanged) {
      const project = state.projects.find((item) => item.id === $("configProject").value);
      if (project?.defaultBuildPlatform) {
        $("configBuildPlatform").value = project.defaultBuildPlatform;
      }
      fillConfigFileDefaults({ forceFileName: true });
    } else {
      fillConfigFileDefaults();
    }
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
    $("jobsList").innerHTML = `<div class="empty-state">暂无任务。选择项目和配置后即可发起打包。</div>`;
    return;
  }

  $("jobsList").innerHTML = `<div class="table-shell jobs-table-shell">
    <table class="data-table jobs-table">
      <thead>
        <tr>
          <th>任务</th>
          <th>状态</th>
          <th>平台</th>
          <th>分支</th>
          <th>Build</th>
          <th>来源</th>
          <th class="table-actions">操作</th>
        </tr>
      </thead>
      <tbody>
        ${state.jobs.map(renderJobRow).join("")}
      </tbody>
    </table>
  </div>`;
}

function renderJobRow(job) {
  const project = state.projects.find((item) => item.id === job.projectId);
  const config = state.configs.find((item) => item.id === job.configId);
  const active = job.status === "Queued" || job.status === "Running";
  return `<tr>
    <td>
      <div class="job-title-cell">
        <strong>${escapeHtml(project?.name || job.projectId)} / ${escapeHtml(config?.name || job.configId)}</strong>
        <div class="muted small">${new Date(job.createdAt).toLocaleString()}</div>
      </div>
    </td>
    <td><span class="status ${escapeHtml(job.status)}">${escapeHtml(statusLabel(job.status))}</span></td>
    <td>${platformBadge(job.buildPlatform || config?.buildPlatform || "ios")}</td>
    <td>${escapeHtml(job.branch || "-")}</td>
    <td>${escapeHtml(job.buildNumber || "-")}</td>
    <td>${escapeHtml(sourceLabel(job.source))}</td>
    <td class="table-actions">
      <button class="secondary" type="button" data-view-job-id="${escapeHtml(job.id)}">查看详情</button>
      ${active ? `<button class="danger" type="button" data-cancel-job-id="${escapeHtml(job.id)}">取消</button>` : ""}
    </td>
  </tr>`;
}

function renderMetrics() {
  const runningJobs = state.jobs.filter((job) => job.status === "Queued" || job.status === "Running").length;
  $("metricProjects").textContent = String(state.projects.length);
  $("metricConfigs").textContent = String(state.configs.length);
  $("metricRunning").textContent = String(runningJobs);
  $("metricJobs").textContent = String(state.jobs.length);
}

async function openJobModal(jobId) {
  stopJobModalPolling();
  state.selectedJobId = jobId;
  $("jobModal").classList.remove("hidden");
  $("jobModal").setAttribute("aria-hidden", "false");
  $("jobModalTitle").textContent = "任务详情";
  $("jobModalSubTitle").textContent = jobId;
  $("jobModalDetail").innerHTML = `<div><strong>状态</strong><br>加载中...</div>`;
  $("jobModalArtifacts").innerHTML = `<article class="item muted">正在加载产物...</article>`;
  $("jobModalLog").textContent = "正在加载日志...";
  try {
    await refreshJobModal(jobId);
  } catch (error) {
    $("jobModalDetail").innerHTML = `<div><strong>状态</strong><br>读取失败</div>`;
    $("jobModalArtifacts").innerHTML = `<article class="item error">产物暂时不可用。</article>`;
    $("jobModalLog").textContent = `日志暂时不可用：${error instanceof Error ? error.message : String(error || "")}`;
    showError(error);
  } finally {
    if (isJobModalOpen() && state.selectedJobId === jobId) {
      startJobModalPolling(jobId);
    }
  }
}

function closeJobModal() {
  stopJobModalPolling();
  $("jobModal").classList.add("hidden");
  $("jobModal").setAttribute("aria-hidden", "true");
  state.selectedJobId = null;
}

function isJobModalOpen() {
  return !$("jobModal").classList.contains("hidden");
}

function startJobModalPolling(jobId) {
  stopJobModalPolling();
  state.jobModalTimer = setInterval(async () => {
    if (!isJobModalOpen() || state.selectedJobId !== jobId) return;
    if (state.jobModalPollInFlight) return;
    state.jobModalPollInFlight = true;
    try {
      await refreshJobModal(jobId);
    } catch {
    } finally {
      state.jobModalPollInFlight = false;
    }
  }, 2000);
}

function stopJobModalPolling() {
  if (state.jobModalTimer) {
    clearInterval(state.jobModalTimer);
    state.jobModalTimer = null;
  }
  state.jobModalPollInFlight = false;
}

async function refreshJobModal(jobId) {
  const encodedJobId = encodeURIComponent(jobId);
  const job = await api(`/api/builds/${encodedJobId}`);

  $("jobModalTitle").textContent = "任务详情";
  $("jobModalSubTitle").textContent = `${job.id} / ${new Date(job.createdAt).toLocaleString()}`;
  const errorText = job.error || "";
  const authError = isGitAuthError(errorText);
  const errorHtml = errorText
    ? `<div class="${authError ? "auth-error-banner" : ""}">${escapeHtml(errorText)}</div>`
    : "-";

  $("jobModalDetail").innerHTML = [
    ["状态", statusLabel(job.status)],
    ["平台", platformLabel(job.buildPlatform || "ios")],
    ["分支", job.branch],
    ["Build Number", job.buildNumber],
    ["Worker", job.workerId || "-"],
    ["开始时间", job.startedAt ? new Date(job.startedAt).toLocaleString() : "-"],
    ["结束时间", job.finishedAt ? new Date(job.finishedAt).toLocaleString() : "-"],
    ["演练模式", job.dryRun ? "是" : "否"],
    ["错误信息", errorHtml],
  ].map(([key, value]) => `<div><strong>${key}</strong><br>${typeof value === "string" ? value : escapeHtml(String(value))}</div>`).join("");

  const [logResult, artifactsResult] = await Promise.allSettled([
    fetchText(`/api/builds/${encodedJobId}/log?full=true&_ts=${Date.now()}`),
    api(`/api/builds/${encodedJobId}/artifacts`),
  ]);

  if (logResult.status === "fulfilled") {
    $("jobModalLog").textContent = logResult.value || (job.status === "Queued" ? "任务排队中，等待 Worker 写入日志..." : "暂无日志");
  } else {
    $("jobModalLog").textContent = `日志暂时不可用，正在重试：${errorMessage(logResult.reason)}`;
  }

  if (artifactsResult.status === "fulfilled") {
    $("jobModalArtifacts").innerHTML = renderArtifactsTable(artifactsResult.value);
  } else {
    $("jobModalArtifacts").innerHTML = `<article class="item error">产物暂时不可用，正在重试：${escapeHtml(errorMessage(artifactsResult.reason))}</article>`;
  }

  $("jobModal").dataset.jobStatus = job.status || "";
}

function errorMessage(error) {
  return error instanceof Error ? error.message : String(error || "未知错误");
}

function isGitAuthError(text) {
  if (!text) return false;
  const patterns = [
    "Git 认证失败",
    "Authentication failed",
    "could not read Username",
    "could not read Password",
    "Invalid username or token",
    "Invalid username or password",
    "Permission denied (publickey)",
    "Support for password authentication was removed",
    "Personal access tokens with read:org",
  ];
  return patterns.some((p) => text.includes(p));
}

function renderArtifactsTable(artifacts) {
  if (!artifacts.length) {
    return `<div class="empty-state compact">暂无可下载产物</div>`;
  }

  return `<div class="table-shell artifacts-table-shell">
    <table class="data-table artifacts-table">
      <thead>
        <tr>
          <th>类型</th>
          <th>路径</th>
          <th class="table-actions">操作</th>
        </tr>
      </thead>
      <tbody>
        ${artifacts.map((artifact) => `<tr>
          <td><span class="role-pill">${escapeHtml(artifactTypeLabel(artifact.type))}</span></td>
          <td class="path-cell">${escapeHtml(artifact.path || "-")}</td>
          <td class="table-actions">
            <a class="download-link" href="/api/artifacts/${encodeURIComponent(artifact.id)}/download" target="_blank" rel="noopener">下载</a>
          </td>
        </tr>`).join("")}
      </tbody>
    </table>
  </div>`;
}

function platformBadge(platform) {
  const value = String(platform || "ios");
  return `<span class="platform ${escapeHtml(value)}">${escapeHtml(platformLabel(value))}</span>`;
}

function renderWorkers(workers) {
  if (workers.length === 0) {
    $("workersList").innerHTML = `<div class="empty-state">暂无 Worker 心跳。Worker 启动后会自动注册并持续上报状态。</div>`;
    return;
  }

  $("workersList").innerHTML = `<div class="table-shell workers-table-shell">
    <table class="data-table workers-table">
      <thead>
        <tr>
          <th>Worker</th>
          <th>状态</th>
          <th>当前任务</th>
          <th>Unity</th>
          <th>Xcode</th>
          <th>项目</th>
          <th>最近心跳</th>
        </tr>
      </thead>
      <tbody>
        ${workers.map(renderWorkerRow).join("")}
      </tbody>
    </table>
  </div>`;
}

function renderWorkerRow(worker) {
  const status = worker.enabled ? (worker.status || "Unknown") : "Disabled";
  const unityVersions = listSummary(worker.unityVersions);
  const xcodeVersions = listSummary(worker.xcodeVersions);
  const projectCount = (worker.projectIds || []).length;
  return `<tr>
    <td>
      <div class="job-title-cell">
        <strong>${escapeHtml(worker.name || worker.id)}</strong>
        <div class="muted small">${escapeHtml(worker.hostName || "-")}</div>
      </div>
    </td>
    <td><span class="status ${escapeHtml(status)}">${escapeHtml(statusLabel(status))}</span></td>
    <td>${escapeHtml(worker.currentJobId || "-")}</td>
    <td>${escapeHtml(unityVersions)}</td>
    <td>${escapeHtml(xcodeVersions)}</td>
    <td>${projectCount}</td>
    <td class="nowrap">${worker.lastSeenAt ? new Date(worker.lastSeenAt).toLocaleString() : "-"}</td>
  </tr>`;
}

function listSummary(values) {
  if (!Array.isArray(values) || values.length === 0) return "-";
  if (values.length <= 2) return values.join(", ");
  return `${values.slice(0, 2).join(", ")} +${values.length - 2}`;
}

async function refreshUsers() {
  if (!isAdmin()) return;
  state.users = await api("/api/users");
  renderUsers();
}

function renderUsers() {
  if (!isAdmin()) return;
  renderUserStats();
  $("userDirectoryCount").textContent = `${state.users.length} 人`;
  if (state.users.length === 0) {
    state.selectedUserId = "";
    $("usersList").innerHTML = `<div class="empty-state compact">暂无用户。点击右上角新增用户。</div>`;
    renderUserDetail(null);
    return;
  }

  if (!state.users.some((user) => user.id === state.selectedUserId)) {
    state.selectedUserId = state.users[0].id;
  }
  $("usersList").innerHTML = state.users.map(renderUserListItem).join("");
  renderUserDetail();
}

function renderUserStats() {
  const total = state.users.length;
  const enabled = state.users.filter((user) => user.enabled).length;
  const admins = state.users.filter((user) => user.role === "Admin").length;
  const protectedUsers = state.users.filter(isRootAdminUser).length;
  $("userStats").innerHTML = [
    ["用户总数", total],
    ["启用账号", enabled],
    ["管理员", admins],
    ["受保护主账号", protectedUsers],
  ].map(([label, value]) => `<article><span>${label}</span><strong>${value}</strong></article>`).join("");
}

function renderUserListItem(user) {
  const selected = user.id === state.selectedUserId;
  const protectedText = isRootAdminUser(user) ? `<span class="protect-note">主账号</span>` : "";
  return `<article class="user-list-item${selected ? " selected" : ""}" role="option" tabindex="0" aria-selected="${selected ? "true" : "false"}" data-select-user-id="${escapeHtml(user.id)}">
    <span class="avatar">${avatarText(user)}</span>
    <span class="user-list-main">
      <span class="user-list-title">
        <strong>${escapeHtml(user.displayName || user.userName)}</strong>
        ${protectedText}
      </span>
      <span class="muted small">${escapeHtml(user.userName)}</span>
    </span>
    <span class="user-list-badges">
      <span class="role-pill">${escapeHtml(roleLabel(user.role))}</span>
      <span class="status ${user.enabled ? "Succeeded" : "Canceled"}">${enabledLabel(user.enabled)}</span>
    </span>
  </article>`;
}

function renderUserDetail(user = state.users.find((item) => item.id === state.selectedUserId)) {
  if (!user) {
    $("userDetailPanel").innerHTML = `<div class="user-detail-empty">
      <p class="eyebrow">账号详情</p>
      <h4>还没有可查看的账号</h4>
      <p class="muted">点击右上角“新增用户”为团队成员分配权限。</p>
    </div>`;
    return;
  }

  const protectedReason = protectedUserReason(user);
  const actionDisabled = protectedReason || !user.enabled ? "disabled" : "";
  const actionTitle = protectedReason || (!user.enabled ? "用户已经禁用。" : "");
  $("userDetailPanel").innerHTML = `<div class="user-detail-header">
      <span class="avatar large">${avatarText(user)}</span>
      <div>
        <p class="eyebrow">当前账号</p>
        <h4>${escapeHtml(user.displayName || user.userName)}</h4>
        <p class="muted">${escapeHtml(user.userName)}</p>
      </div>
      <span class="status ${user.enabled ? "Succeeded" : "Canceled"}">${enabledLabel(user.enabled)}</span>
    </div>
    <dl class="user-detail-grid">
      <div>
        <dt>角色</dt>
        <dd><span class="role-pill">${escapeHtml(roleLabel(user.role))}</span></dd>
      </div>
      <div>
        <dt>账号类型</dt>
        <dd>${isRootAdminUser(user) ? "受保护主账号" : "普通团队账号"}</dd>
      </div>
      <div>
        <dt>创建时间</dt>
        <dd>${user.createdAt ? new Date(user.createdAt).toLocaleString() : "-"}</dd>
      </div>
      <div>
        <dt>账号 ID</dt>
        <dd class="path-cell">${escapeHtml(user.id)}</dd>
      </div>
    </dl>
    <section class="permission-card">
      <p class="eyebrow">权限范围</p>
      <strong>${escapeHtml(roleLabel(user.role))}</strong>
      <p>${escapeHtml(roleDescription(user.role))}</p>
      ${protectedReason ? `<p class="protect-warning">${escapeHtml(protectedReason)}</p>` : ""}
    </section>
    <div class="user-detail-actions">
      <button class="secondary" type="button" data-edit-user-id="${escapeHtml(user.id)}">编辑用户</button>
      <button class="danger" type="button" data-disable-user-id="${escapeHtml(user.id)}" title="${escapeHtml(actionTitle)}" ${actionDisabled}>停用/删除</button>
    </div>`;
}

function roleDescription(role) {
  return {
    Admin: "拥有系统配置、用户权限、项目配置、打包任务和审计查看的完整权限。",
    ProjectOwner: "可以维护项目与配置，也可以发起和管理构建任务。",
    Builder: "可以发起构建、查看任务详情、日志和产物。",
    Viewer: "只读查看项目、任务、日志和产物，不能修改配置。",
    Agent: "自动化 Agent 使用的服务账号，通常不用于人工登录。",
  }[role] || "自定义角色，按后端授权规则执行。";
}

function avatarText(user) {
  const source = (user.displayName || user.userName || "U").trim();
  return escapeHtml(source.slice(0, 1).toUpperCase());
}

function protectedUserReason(user) {
  if (isRootAdminUser(user)) return "主账号不可删除或禁用。";
  if (user.id === state.user?.id) return "不能禁用当前登录账号。";
  return "";
}

function isRootAdminUser(user) {
  return (user.userName || "").toLowerCase() === "admin";
}

async function saveUser(event) {
  event.preventDefault();
  clearMessage();
  const userId = $("userId").value;
  const path = userId ? `/api/users/${encodeURIComponent(userId)}` : "/api/users";
  const method = userId ? "PUT" : "POST";
  setButtonBusy("userSaveBtn", true, userId ? "更新中..." : "保存中...");
  try {
    const savedUser = await api(path, {
      method,
      body: JSON.stringify({
        userName: $("userName").value,
        displayName: $("userDisplayName").value,
        role: $("userRole").value,
        password: $("userPassword").value || null,
        enabled: $("userEnabled").checked,
      }),
    });
    state.selectedUserId = savedUser?.id || userId || state.selectedUserId;
    resetUserForm();
    closeUserModal();
    await refreshUsers();
    showMessage(userId ? "用户已更新。" : "用户已创建。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("userSaveBtn", false);
  }
}

function handleUsersListClick(event) {
  if (!event.target.closest) return;
  const selectButton = event.target.closest("[data-select-user-id]");
  if (selectButton) {
    selectUser(selectButton.dataset.selectUserId);
    return;
  }

  const editButton = event.target.closest("[data-edit-user-id]");
  if (editButton) {
    openUserModal(editButton.dataset.editUserId);
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

function handleUsersKeydown(event) {
  if (event.key !== "Enter" && event.key !== " ") return;
  if (!event.target.closest) return;
  const selectItem = event.target.closest("[data-select-user-id]");
  if (!selectItem) return;
  event.preventDefault();
  selectUser(selectItem.dataset.selectUserId);
}

function selectUser(userId) {
  if (!state.users.some((user) => user.id === userId)) return;
  state.selectedUserId = userId;
  renderUsers();
}

async function disableUser(userId) {
  const user = state.users.find((item) => item.id === userId);
  const reason = user ? protectedUserReason(user) : "";
  if (reason) {
    showError(new Error(reason));
    return;
  }

  await api(`/api/users/${encodeURIComponent(userId)}`, { method: "DELETE" });
  if ($("userId").value === userId) {
    resetUserForm();
  }
  state.selectedUserId = userId;
  await refreshUsers();
  showMessage("用户已停用。系统保留账号审计记录。");
}

function openUserModal(userId = "") {
  resetUserForm();
  if (userId) {
    state.selectedUserId = userId;
    fillUserForm(userId);
  }
  $("userModal").classList.remove("hidden");
  $("userModal").setAttribute("aria-hidden", "false");
  setTimeout(() => $("userName").focus(), 0);
}

function closeUserModal() {
  $("userModal").classList.add("hidden");
  $("userModal").setAttribute("aria-hidden", "true");
}

function isUserModalOpen() {
  return !$("userModal").classList.contains("hidden");
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
  const rootAdmin = isRootAdminUser(user);
  $("userName").disabled = rootAdmin;
  $("userRole").disabled = rootAdmin;
  $("userEnabled").disabled = rootAdmin || user.id === state.user?.id;
  $("userEnabled").parentElement.title = rootAdmin ? "主账号必须保持管理员角色且启用。" : ($("userEnabled").disabled ? "不能禁用当前登录账号。" : "");
  $("userModalTitle").textContent = "编辑用户";
  $("userModalSubTitle").textContent = "密码留空表示不修改。";
  $("userSaveBtn").textContent = "更新用户";
  $("userSaveBtn").dataset.defaultText = "更新用户";
}

function resetUserForm() {
  $("userForm").reset();
  $("userId").value = "";
  $("userRole").value = "Builder";
  $("userEnabled").checked = true;
  $("userName").disabled = false;
  $("userRole").disabled = false;
  $("userEnabled").disabled = false;
  $("userEnabled").parentElement.title = "";
  $("userModalTitle").textContent = "新增用户";
  $("userModalSubTitle").textContent = "为团队成员分配最小必要权限。";
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
    $("auditList").innerHTML = `<div class="empty-state">暂无审计记录。</div>`;
    return;
  }

  $("auditList").innerHTML = `<div class="table-shell audit-table-shell">
    <table class="data-table audit-table">
      <thead>
        <tr>
          <th>动作</th>
          <th>用户</th>
          <th>目标</th>
          <th>详情</th>
          <th>时间</th>
        </tr>
      </thead>
      <tbody>
        ${audit.map(renderAuditRow).join("")}
      </tbody>
    </table>
  </div>`;
}

function renderAuditRow(item) {
  return `<tr>
    <td><span class="role-pill">${escapeHtml(auditActionLabel(item.action))}</span></td>
    <td>${escapeHtml(item.userName || "-")}</td>
    <td>
      <div class="job-title-cell">
        <strong>${escapeHtml(auditTargetLabel(item.targetType))}</strong>
        <div class="muted small">${escapeHtml(item.targetId || "-")}</div>
      </div>
    </td>
    <td class="audit-detail">${escapeHtml(item.details || "-")}</td>
    <td class="nowrap">${item.createdAt ? new Date(item.createdAt).toLocaleString() : "-"}</td>
  </tr>`;
}

function statusLabel(value) {
  return labelFromMap(STATUS_LABELS, value);
}

function roleLabel(value) {
  return labelFromMap(ROLE_LABELS, value);
}

function platformLabel(value) {
  return labelFromMap(PLATFORM_LABELS, value || "ios");
}

function sourceLabel(value) {
  return value ? labelFromMap(SOURCE_LABELS, value) : "-";
}

function artifactTypeLabel(value) {
  return labelFromMap(ARTIFACT_TYPE_LABELS, value);
}

function auditActionLabel(value) {
  return labelFromMap(AUDIT_ACTION_LABELS, value);
}

function auditTargetLabel(value) {
  return value ? labelFromMap(AUDIT_TARGET_LABELS, value) : "-";
}

function enabledLabel(enabled) {
  return enabled ? "已启用" : "已停用";
}

function labelFromMap(map, value) {
  const text = String(value ?? "");
  if (!text) return "-";
  const direct = map[text] || map[text.toLowerCase()] || map[text.toUpperCase()];
  if (direct) return direct;
  const matchedKey = Object.keys(map).find((key) => key.toLowerCase() === text.toLowerCase());
  return matchedKey ? map[matchedKey] : text;
}

function loadingItem(text) {
  return `<article class="item loading"><span class="spinner"></span><span>${escapeHtml(text)}</span></article>`;
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

async function loadEmailSettings() {
  const settings = await api("/api/email-settings");
  state.emailSettings = settings;
  $("emailEnabled").checked = Boolean(settings.enabled);
  $("smtpHost").value = settings.smtpHost || "";
  $("smtpPort").value = settings.smtpPort || 587;
  $("smtpUserName").value = settings.smtpUserName || "";
  $("smtpPassword").value = "";
  $("fromEmail").value = settings.fromEmail || "";
  $("fromName").value = settings.fromName || "";
  $("useSsl").checked = settings.useSsl !== false;
}

async function saveEmailSettings(event) {
  event.preventDefault();
  clearMessage();
  setButtonBusy("emailSaveBtn", true, "保存中...");
  try {
    await api("/api/email-settings", {
      method: "PUT",
      body: JSON.stringify({
        smtpHost: $("smtpHost").value,
        smtpPort: parseInt($("smtpPort").value, 10) || 587,
        smtpUserName: $("smtpUserName").value,
        smtpPassword: $("smtpPassword").value || null,
        fromEmail: $("fromEmail").value,
        fromName: $("fromName").value || null,
        useSsl: $("useSsl").checked,
        enabled: $("emailEnabled").checked,
      }),
    });
    $("smtpPassword").value = "";
    showMessage("邮件通知设置已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("emailSaveBtn", false);
  }
}

async function sendTestEmail() {
  clearMessage();
  const toEmail = $("testEmailTo").value.trim();
  if (!toEmail) {
    showError(new Error("请填写收件邮箱。"));
    return;
  }

  setButtonBusy("emailTestBtn", true, "发送中...");
  const resultEl = $("emailTestResult");
  resultEl.classList.remove("hidden");
  resultEl.className = "toast";
  resultEl.textContent = "正在发送测试邮件...";
  try {
    const result = await api("/api/email-settings/test", {
      method: "POST",
      body: JSON.stringify({ toEmail }),
    });
    if (result.ok) {
      resultEl.className = "toast";
      resultEl.textContent = "测试邮件已发送，请检查收件箱。";
    } else {
      resultEl.className = "toast error";
      resultEl.textContent = `发送失败：${result.error || "未知错误"}`;
    }
  } catch (error) {
    resultEl.className = "toast error";
    resultEl.textContent = `发送失败：${error.message || error}`;
  } finally {
    setButtonBusy("emailTestBtn", false);
  }
}

function formatBytes(bytes) {
  if (!bytes || bytes === 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let i = 0;
  let size = bytes;
  while (size >= 1024 && i < units.length - 1) {
    size /= 1024;
    i++;
  }
  return `${size.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

async function loadStorageData() {
  await Promise.all([loadStorageOverview(), loadStorageJobs()]);
}

async function loadStorageOverview() {
  const overview = await api("/api/storage/overview");
  state.storageOverview = overview;
  renderStorageOverview();
}

async function loadStorageJobs() {
  const status = $("storageStatusFilter").value;
  const url = status ? `/api/storage/jobs?status=${encodeURIComponent(status)}` : "/api/storage/jobs";
  const jobs = await api(url);
  state.storageJobs = jobs;
  state.selectedStorageJobIds = new Set();
  renderStorageJobs();
}

function renderStorageOverview() {
  const o = state.storageOverview;
  if (!o) return;
  $("storageTotalJobs").textContent = String(o.totalJobs ?? 0);
  $("storageCompletedJobs").textContent = String(o.completedJobs ?? 0);
  $("storageArtifactBytes").textContent = formatBytes(o.totalArtifactBytes ?? 0);
  $("storageLogBytes").textContent = formatBytes(o.totalLogBytes ?? 0);
  $("storageArtifactCount").textContent = String(o.artifactCount ?? 0);
  const retention = o.retentionDays > 0 ? `${o.retentionDays} 天` : "未启用";
  const quota = o.maxArtifactBytes > 0 ? formatBytes(o.maxArtifactBytes) : "不限";
  $("storagePolicy").textContent = `${retention} / ${quota}`;
}

function renderStorageJobs() {
  if (!state.storageJobs || state.storageJobs.length === 0) {
    $("storageJobsList").innerHTML = `<div class="empty-state">暂无任务产物记录。</div>`;
    return;
  }

  $("storageJobsList").innerHTML = `<div class="table-shell">
    <table class="data-table">
      <thead>
        <tr>
          <th style="width: 40px;"><input type="checkbox" id="storageMasterCheckbox"></th>
          <th>项目/配置</th>
          <th>状态</th>
          <th>平台</th>
          <th>Build</th>
          <th>产物大小</th>
          <th>文件数</th>
          <th>完成时间</th>
          <th class="table-actions">操作</th>
        </tr>
      </thead>
      <tbody>
        ${state.storageJobs.map(renderStorageJobRow).join("")}
      </tbody>
    </table>
  </div>`;

  $("storageMasterCheckbox")?.addEventListener("change", (e) => {
    if (e.target.checked) {
      state.storageJobs.forEach((job) => {
        if (job.hasFilesOnDisk) state.selectedStorageJobIds.add(job.jobId);
      });
    } else {
      state.selectedStorageJobIds.clear();
    }
    renderStorageJobs();
  });
}

function renderStorageJobRow(job) {
  const checked = state.selectedStorageJobIds.has(job.jobId);
  const disabled = job.hasFilesOnDisk ? "" : "disabled title=\"没有磁盘文件可清理\"";
  return `<tr>
    <td><input type="checkbox" class="storage-job-checkbox" data-job-id="${escapeHtml(job.jobId)}" ${checked ? "checked" : ""} ${disabled}></td>
    <td>
      <div class="job-title-cell">
        <strong>${escapeHtml(job.projectName)} / ${escapeHtml(job.configName)}</strong>
      </div>
    </td>
    <td><span class="status ${escapeHtml(job.status)}">${escapeHtml(statusLabel(job.status))}</span></td>
    <td>${platformBadge(job.platform || "ios")}</td>
    <td>${escapeHtml(job.buildNumber || "-")}</td>
    <td>${formatBytes(job.artifactBytes || 0)}</td>
    <td>${job.artifactCount || 0}</td>
    <td class="nowrap">${job.finishedAt ? new Date(job.finishedAt).toLocaleString() : "-"}</td>
    <td class="table-actions">
      <button class="danger" type="button" data-delete-storage-id="${escapeHtml(job.jobId)}" ${disabled}>删除</button>
    </td>
  </tr>`;
}

function toggleSelectAllStorageJobs() {
  const allSelected = state.storageJobs.length > 0 &&
    state.storageJobs.every((job) => !job.hasFilesOnDisk || state.selectedStorageJobIds.has(job.jobId));
  if (allSelected) {
    state.selectedStorageJobIds.clear();
  } else {
    state.storageJobs.forEach((job) => {
      if (job.hasFilesOnDisk) state.selectedStorageJobIds.add(job.jobId);
    });
  }
  renderStorageJobs();
}

async function batchDeleteStorage() {
  const jobIds = Array.from(state.selectedStorageJobIds);
  if (jobIds.length === 0) {
    showError(new Error("请先勾选要删除的任务。"));
    return;
  }

  if (!confirm(`确认删除选中的 ${jobIds.length} 个任务的产物文件和日志？\n任务记录会保留用于审计，但磁盘上的 ipa/apk/日志等文件会被永久删除。`)) {
    return;
  }

  setButtonBusy("storageDeleteBtn", true, "删除中...");
  try {
    const result = await api("/api/storage/cleanup", {
      method: "POST",
      body: JSON.stringify({ jobIds }),
    });
    state.selectedStorageJobIds.clear();
    await loadStorageData();
    const msg = result.errors && result.errors.length > 0
      ? `已删除 ${result.deleted} 个任务，${result.errors.length} 个失败：${result.errors.join("; ")}`
      : `已删除 ${result.deleted} 个任务的产物文件。`;
    showMessage(msg);
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("storageDeleteBtn", false);
  }
}

async function deleteSingleStorageJob(jobId) {
  const job = state.storageJobs.find((j) => j.jobId === jobId);
  if (!job) return;

  if (!confirm(`确认删除「${job.projectName} / ${job.configName}」(#${job.buildNumber}) 的产物文件和日志？\n任务记录会保留用于审计，但磁盘上的 ipa/apk/日志等文件会被永久删除。`)) {
    return;
  }

  try {
    await api(`/api/storage/jobs/${encodeURIComponent(jobId)}`, { method: "DELETE" });
    state.selectedStorageJobIds.delete(jobId);
    await loadStorageData();
    showMessage("产物文件已删除。");
  } catch (error) {
    showError(error);
  }
}

function renderContacts() {
  if (state.notificationContacts.length === 0) {
    $("contactsList").innerHTML = `<div class="empty-state compact">暂无通知联系人。使用上方表单添加负责人或测试人员邮箱。</div>`;
    return;
  }

  $("contactsList").innerHTML = `<div class="table-shell">
    <table class="data-table">
      <thead>
        <tr>
          <th>职位名称</th>
          <th>邮箱</th>
          <th>状态</th>
          <th class="table-actions">操作</th>
        </tr>
      </thead>
      <tbody>
        ${state.notificationContacts.map((contact) => `<tr>
          <td><strong>${escapeHtml(contact.title)}</strong></td>
          <td>${escapeHtml(contact.email)}</td>
          <td><span class="status ${contact.enabled ? "Succeeded" : "Canceled"}">${enabledLabel(contact.enabled)}</span></td>
          <td class="table-actions">
            <button class="secondary" type="button" data-edit-contact-id="${escapeHtml(contact.id)}">编辑</button>
            <button class="danger" type="button" data-delete-contact-id="${escapeHtml(contact.id)}">删除</button>
          </td>
        </tr>`).join("")}
      </tbody>
    </table>
  </div>`;
}

async function saveNotificationContact(event) {
  event.preventDefault();
  clearMessage();
  const contactId = $("contactId").value;
  const isEditing = Boolean(contactId);
  setButtonBusy("contactSaveBtn", true, isEditing ? "更新中..." : "添加中...");
  try {
    const path = isEditing
      ? `/api/notification-contacts/${encodeURIComponent(contactId)}`
      : "/api/notification-contacts";
    await api(path, {
      method: isEditing ? "PUT" : "POST",
      body: JSON.stringify({
        title: $("contactTitle").value,
        email: $("contactEmail").value,
        enabled: $("contactEnabled").checked,
      }),
    });
    resetContactForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage(isEditing ? "联系人已更新。" : "联系人已添加。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("contactSaveBtn", false);
  }
}

function handleContactsListClick(event) {
  if (!event.target.closest) return;
  const editBtn = event.target.closest("[data-edit-contact-id]");
  if (editBtn) {
    editContact(editBtn.dataset.editContactId);
    return;
  }
  const deleteBtn = event.target.closest("[data-delete-contact-id]");
  if (deleteBtn) {
    deleteNotificationContact(deleteBtn.dataset.deleteContactId);
  }
}

function editContact(contactId) {
  const contact = state.notificationContacts.find((c) => c.id === contactId);
  if (!contact) return;
  $("contactId").value = contact.id;
  $("contactTitle").value = contact.title;
  $("contactEmail").value = contact.email;
  $("contactEnabled").checked = Boolean(contact.enabled);
  $("contactSaveBtn").textContent = "更新联系人";
  $("contactSaveBtn").dataset.defaultText = "更新联系人";
  $("contactCancelBtn").classList.remove("hidden");
  $("contactForm").scrollIntoView({ behavior: "smooth", block: "start" });
}

function resetContactForm() {
  $("contactForm").reset();
  $("contactId").value = "";
  $("contactEnabled").checked = true;
  $("contactSaveBtn").textContent = "添加联系人";
  $("contactSaveBtn").dataset.defaultText = "添加联系人";
  $("contactCancelBtn").classList.add("hidden");
}

async function deleteNotificationContact(contactId) {
  await api(`/api/notification-contacts/${encodeURIComponent(contactId)}`, { method: "DELETE" });
  if ($("contactId").value === contactId) {
    resetContactForm();
  }
  await refreshAll({ showSuccess: false, throwOnError: true });
  showMessage("联系人已删除。");
}

function startGatewayAgentPolling() {
  refreshGatewayAgentStatus().catch(() => {});
  state.gatewayAgentTimer = setInterval(() => {
    refreshGatewayAgentStatus().catch(() => {});
  }, 5000);
}

async function refreshGatewayAgentStatus() {
  try {
    const status = await api("/api/gateway-agent/status");
    updateGatewayAgentPill(status);
    if (state.activeTab === "gateway") {
      updateGatewayStatusDetail(status);
    }
  } catch {
  }
}

function updateGatewayAgentPill(status) {
  const pill = $("gatewayAgentPill");
  if (!pill) return;
  const statusMap = {
    Connected: { text: "Gateway 已连接", cls: "connected" },
    Connecting: { text: "Gateway 建立连接中", cls: "connecting" },
    Reconnecting: { text: "Gateway 等待重连", cls: "connecting" },
    Disconnected: { text: "Gateway 未连接", cls: "disconnected" },
    Failed: { text: status?.requiresEnrollment ? "Gateway 凭据已失效" : "Gateway 连接失败", cls: "failed" },
  };
  const info = statusMap[status?.status] || statusMap.Disconnected;
  pill.textContent = info.text;
  pill.className = `live-status gateway-pill ${info.cls}`;
  if (status?.isConnected && status.nodeId) {
    pill.textContent += ` · ${status.nodeId.slice(-8)}`;
  }
  if (status?.lastError) {
    pill.title = status.lastError;
  } else {
    pill.title = "Gateway 连接状态";
  }
}

function updateGatewayStatusDetail(status) {
  $("gwStatus").textContent = describeGatewayStatus(status);
  $("gwNodeId").textContent = status.nodeId || "-";
  $("gwUrl").textContent = status.gatewayUrl || "-";
  $("gwHeartbeat").textContent = formatGatewayTime(status.lastHeartbeatAt);
  $("gwLastAttempt").textContent = formatGatewayTime(status.lastAttemptAt);
  $("gwLastConnected").textContent = formatGatewayTime(status.lastConnectedAt);
  $("gwNextRetry").textContent = formatGatewayTime(status.nextRetryAt);
  $("gwReconnect").textContent = String(status.reconnectAttempts || 0);
  const errorEl = $("gwStatusError");
  const errorText = gatewayStatusMessage(status);
  if (errorText) {
    errorEl.textContent = errorText;
    errorEl.classList.remove("hidden");
  } else {
    errorEl.textContent = "";
    errorEl.classList.add("hidden");
  }

  const disconnectBtn = $("gwDisconnectBtn");
  if (disconnectBtn) {
    disconnectBtn.disabled = !status.hasCredential && !status.isConnected && !status.nodeId;
  }

  if (status.gatewayUrl && !$("gwGatewayUrl").value) {
    $("gwGatewayUrl").value = status.gatewayUrl;
  }
}

function describeGatewayStatus(status) {
  const statusMap = {
    Connected: "已连接",
    Connecting: "建立连接中",
    Reconnecting: "等待重连",
    Disconnected: "未连接",
    Failed: status?.requiresEnrollment ? "凭据失效" : "连接失败",
  };
  return statusMap[status?.status] || "未知";
}

function gatewayStatusMessage(status) {
  if (!status?.lastError) return "";
  if (status.requiresEnrollment) {
    return `${status.lastError} 旧凭据已不可用，请在 LinuxGateway 重新生成 Enrollment Token 后重新连接。`;
  }
  return status.lastError;
}

function formatGatewayTime(value) {
  return value ? new Date(value).toLocaleString() : "-";
}

async function connectGateway(event) {
  event.preventDefault();
  clearMessage();
  const errorEl = $("gwConnectError");
  errorEl.classList.add("hidden");
  setButtonBusy("gwConnectBtn", true, "连接中...");
  try {
    const result = await api("/api/gateway-agent/connect", {
      method: "POST",
      body: JSON.stringify({
        gatewayUrl: $("gwGatewayUrl").value,
        enrollmentToken: $("gwEnrollmentToken").value,
        autoConnect: $("gwAutoConnect").checked,
      }),
    });
    if (result.status) {
      updateGatewayAgentPill(result.status);
      updateGatewayStatusDetail(result.status);
    }
    const statusText = result.status?.isConnected
      ? "WebSocket 已连接"
      : "已完成注册，正在建立 WebSocket 连接";
    showMessage(`${statusText}，节点 ID: ${result.nodeId}`);
    $("gwEnrollmentToken").value = "";
    await refreshGatewayAgentStatus();
  } catch (error) {
    errorEl.textContent = error.message || String(error);
    errorEl.classList.remove("hidden");
  } finally {
    setButtonBusy("gwConnectBtn", false);
  }
}

async function disconnectGateway() {
  setButtonBusy("gwDisconnectBtn", true, "断开中...");
  try {
    const result = await api("/api/gateway-agent/disconnect", { method: "POST" });
    if (result.status) {
      updateGatewayAgentPill(result.status);
      updateGatewayStatusDetail(result.status);
    }
    $("gwEnrollmentToken").value = "";
    showMessage("已断开 Gateway 连接，并清除本机保存的节点凭据。");
    await refreshGatewayAgentStatus();
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("gwDisconnectBtn", false);
  }
}

// ---- Quick Fill ----

function autoFillProjectFields(projectId) {
  if (!projectId || state.editingConfigId) return;
  const profile = state.projectProfiles.find((p) => p.projectRecordId === projectId || p.id === projectId);
  if (!profile) return;
  if (profile.repositoryUrl) $("configProjectDirectoryName").value = deriveRepoFolderName(profile.repositoryUrl) || profile.projectDirectoryName || "";
}

function renderQuickFillDropdowns() {
  const unityOptions = ['<option value="">不使用</option>']
    .concat(state.unityProjectProfiles.map((u) => `<option value="${escapeHtml(u.id)}">${escapeHtml(u.name)}</option>`))
    .join("");
  $("quickFillUnity").innerHTML = unityOptions;

  const signingOptions = ['<option value="">不使用</option>']
    .concat(state.signingProfiles.map((s) => `<option value="${escapeHtml(s.id)}">${escapeHtml(s.name)}</option>`))
    .join("");
  $("quickFillSigning").innerHTML = signingOptions;

  const certOptions = ['<option value="">不使用</option>']
    .concat(state.certificateProfiles.map((c) => `<option value="${escapeHtml(c.id)}">${escapeHtml(c.name)}</option>`))
    .join("");
  $("quickFillCert").innerHTML = certOptions;
}

function applyTemplateFill(type) {
  if (state.editingConfigId) return;

  if (type === "unity") {
    const unityId = $("quickFillUnity").value;
    const unitySection = $("unitySection");
    if (!unityId) {
      unitySection?.classList.remove("hidden");
      return;
    }
    const unity = state.unityProjectProfiles.find((u) => u.id === unityId);
    if (!unity) return;
    if (unity.unityProjectRelativePath) $("configUnityRelativePath").value = unity.unityProjectRelativePath;
    if (unity.unityVersion) $("configUnityVersion").value = unity.unityVersion;
    if (unity.unityExecutablePath) $("configUnityExecutablePath").value = unity.unityExecutablePath;
    if (unity.unityBuildMethod) $("configUnityBuildMethod").value = unity.unityBuildMethod;
    if (unity.productName) $("configProductName").value = unity.productName;
    if (unity.bundleIdentifier) $("configBundleIdentifier").value = unity.bundleIdentifier;
    unitySection?.classList.add("hidden");
    showMessage(`已从工程模板「${unity.name}」填充 Unity 工程设置。`);
  }

  if (type === "signing") {
    const signingId = $("quickFillSigning").value;
    const iosSigning = $("iosSigningSection");
    const androidSigning = $("androidSigningSection");
    if (!signingId) {
      iosSigning?.classList.remove("hidden");
      androidSigning?.classList.remove("hidden");
      return;
    }
    const signing = state.signingProfiles.find((s) => s.id === signingId);
    if (!signing) return;
    if (signing.teamId) $("configTeamId").value = signing.teamId;
    if (signing.exportMethod) $("configExportMethod").value = signing.exportMethod;
    if (signing.signingStyle) $("configSigningStyle").value = signing.signingStyle;
    if (signing.iosDeploymentTarget) $("configIosDeploymentTarget").value = signing.iosDeploymentTarget;
    if (signing.androidKeystoreName) $("configAndroidKeystoreName").value = signing.androidKeystoreName;
    if (signing.androidKeystorePass) $("configAndroidKeystorePass").value = signing.androidKeystorePass;
    if (signing.androidKeyaliasName) $("configAndroidKeyaliasName").value = signing.androidKeyaliasName;
    if (signing.androidKeyaliasPass) $("configAndroidKeyaliasPass").value = signing.androidKeyaliasPass;
    const platform = $("configBuildPlatform").value;
    if (platform === "ios" || platform === "tiktok") iosSigning?.classList.add("hidden");
    if (platform === "android") androidSigning?.classList.add("hidden");
    if (platform === "all" || signing.platform === "all") {
      iosSigning?.classList.add("hidden");
      androidSigning?.classList.add("hidden");
    }
    showMessage(`已从签名模板「${signing.name}」填充签名设置。`);
  }

  if (type === "cert") {
    const certId = $("quickFillCert").value;
    const ascSection = $("appStoreConnectSection");
    const gpSection = $("googlePlaySection");
    if (!certId) {
      ascSection?.classList.remove("hidden");
      gpSection?.classList.remove("hidden");
      return;
    }
    const cert = state.certificateProfiles.find((c) => c.id === certId);
    if (!cert) return;
    if (cert.appStoreConnectApiKeyPath) $("configAppStoreConnectApiKeyPath").value = cert.appStoreConnectApiKeyPath;
    if (cert.appStoreConnectApiKeyId) $("configAppStoreConnectApiKeyId").value = cert.appStoreConnectApiKeyId;
    if (cert.appStoreConnectApiIssuerId) $("configAppStoreConnectApiIssuerId").value = cert.appStoreConnectApiIssuerId;
    $("configAppStoreConnectUploadEnabled").checked = Boolean(cert.appStoreConnectUploadEnabled);
    if (cert.googlePlayPackageName) $("configGooglePlayPackageName").value = cert.googlePlayPackageName;
    if (cert.googlePlayServiceAccountJsonPath) $("configGooglePlayServiceAccountJsonPath").value = cert.googlePlayServiceAccountJsonPath;
    if (cert.googlePlayTrack) $("configGooglePlayTrack").value = cert.googlePlayTrack;
    $("configGooglePlayUploadEnabled").checked = Boolean(cert.googlePlayUploadEnabled);
    if (cert.tiktokAppId) $("configTiktokAppId").value = cert.tiktokAppId;
    if (cert.tiktokAccessToken) $("configTiktokAccessToken").value = cert.tiktokAccessToken;
    if (cert.tiktokGameName) $("configTiktokGameName").value = cert.tiktokGameName;
    if (cert.tiktokApiEndpoint) $("configTiktokApiEndpoint").value = cert.tiktokApiEndpoint;
    $("configTiktokUploadEnabled").checked = Boolean(cert.tiktokUploadEnabled);
    toggleUploadSections();
    if (cert.appStoreConnectUploadEnabled) ascSection?.classList.add("hidden");
    if (cert.googlePlayUploadEnabled) gpSection?.classList.add("hidden");
    showMessage(`已从证书模板「${cert.name}」填充上传配置。`);
  }
}

// ---- Project Profiles ----

async function saveProjectProfile(event) {
  event.preventDefault();
  clearMessage();
  const profileId = $("projectProfileId").value;
  const isEditing = Boolean(profileId);
  setButtonBusy("projectProfileSaveBtn", true, isEditing ? "更新中..." : "保存中...");
  try {
    const path = isEditing ? `/api/project-profiles/${encodeURIComponent(profileId)}` : "/api/project-profiles";
    const method = isEditing ? "PUT" : "POST";
    await api(path, {
      method,
      body: JSON.stringify({
        name: $("projectProfileName").value,
        repositoryUrl: $("projectProfileRepo").value || null,
        defaultBranch: $("projectProfileBranch").value || null,
        allowedBranches: ($("projectProfileAllowedBranches").value || "").split(",").map((item) => item.trim()).filter(Boolean),
        defaultBuildPlatform: $("projectProfileDefaultPlatform").value,
        description: $("projectProfileDescription").value || null,
        projectDirectoryName: $("projectProfileDirName").value || null,
        workspaceRoot: $("projectProfileWorkspace").value || null,
        artifactsRoot: $("projectProfileArtifacts").value || null,
      }),
    });
    resetProjectProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage(isEditing ? "项目已更新。" : "项目已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("projectProfileSaveBtn", false);
  }
}

function handleProjectProfilesListClick(event) {
  if (!event.target.closest) return;
  const editBtn = event.target.closest("[data-edit-pp-id]");
  if (editBtn) {
    editProjectProfile(editBtn.dataset.editPpId);
    return;
  }
  const deleteBtn = event.target.closest("[data-delete-pp-id]");
  if (deleteBtn) {
    deleteProjectProfile(deleteBtn.dataset.deletePpId);
  }
}

function editProjectProfile(profileId) {
  const profile = state.projectProfiles.find((p) => p.id === profileId);
  if (!profile) return;
  $("projectProfileId").value = profile.id;
  $("projectProfileName").value = profile.name || "";
  $("projectProfileRepo").value = profile.repositoryUrl || "";
  $("projectProfileBranch").value = profile.defaultBranch || "main";
  $("projectProfileAllowedBranches").value = (profile.allowedBranches || ["main"]).join(",");
  $("projectProfileDefaultPlatform").value = profile.defaultBuildPlatform || "ios";
  $("projectProfileDescription").value = profile.description || "";
  $("projectProfileDirName").value = profile.projectDirectoryName || "";
  $("projectProfileWorkspace").value = profile.workspaceRoot || "~/UnityBuildWorkspace";
  $("projectProfileArtifacts").value = profile.artifactsRoot || "~/UnityBuildArtifacts";
  $("projectProfileFormTitle").textContent = "编辑项目";
  $("projectProfileSaveBtn").textContent = "更新项目";
  $("projectProfileCancelBtn").classList.remove("hidden");
  $("projectProfileForm").scrollIntoView({ behavior: "smooth", block: "start" });
}

function resetProjectProfileForm() {
  $("projectProfileForm").reset();
  $("projectProfileId").value = "";
  $("projectProfileBranch").value = "main";
  $("projectProfileAllowedBranches").value = "main";
  $("projectProfileDefaultPlatform").value = "ios";
  $("projectProfileWorkspace").value = "~/UnityBuildWorkspace";
  $("projectProfileArtifacts").value = "~/UnityBuildArtifacts";
  $("projectProfileFormTitle").textContent = "新增项目";
  $("projectProfileSaveBtn").textContent = "保存项目";
  $("projectProfileCancelBtn").classList.add("hidden");
}

async function deleteProjectProfile(profileId) {
  if (!confirm("确认删除这个项目？关联的配置和任务记录会保留，但项目将从列表移除。")) return;
  try {
    await api(`/api/project-profiles/${encodeURIComponent(profileId)}`, { method: "DELETE" });
    if ($("projectProfileId").value === profileId) resetProjectProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage("项目已删除。");
  } catch (error) {
    showError(error);
  }
}

function renderProjectProfiles() {
  if (state.projectProfiles.length === 0) {
    $("projectProfilesList").innerHTML = `<div class="empty-state compact">暂无项目。使用左侧表单添加。</div>`;
    return;
  }

  $("projectProfilesList").innerHTML = state.projectProfiles.map((p) => `<article class="item">
    <header>
      <div>
        <strong>${escapeHtml(p.name)}</strong>
        <div class="muted small">${escapeHtml(p.repositoryUrl || "-")}</div>
      </div>
      <div class="item-actions">
        <button class="secondary" type="button" data-edit-pp-id="${escapeHtml(p.id)}">编辑</button>
        <button class="danger" type="button" data-delete-pp-id="${escapeHtml(p.id)}">删除</button>
      </div>
    </header>
    <dl class="project-meta">
      <div><dt>默认分支</dt><dd>${escapeHtml(p.defaultBranch || "-")}</dd></div>
      <div><dt>默认平台</dt><dd>${platformBadge(p.defaultBuildPlatform || "ios")}</dd></div>
      <div><dt>Workspace</dt><dd>${escapeHtml(p.workspaceRoot || "-")}</dd></div>
    </dl>
  </article>`).join("");
}

// ---- Unity Project Profiles ----

async function saveUnityProfile(event) {
  event.preventDefault();
  clearMessage();
  const profileId = $("unityProfileId").value;
  const isEditing = Boolean(profileId);
  setButtonBusy("unityProfileSaveBtn", true, isEditing ? "更新中..." : "保存中...");
  try {
    const path = isEditing ? `/api/unity-project-profiles/${encodeURIComponent(profileId)}` : "/api/unity-project-profiles";
    const method = isEditing ? "PUT" : "POST";
    await api(path, {
      method,
      body: JSON.stringify({
        name: $("unityProfileName").value,
        unityProjectRelativePath: $("unityProfileUnityPath").value || null,
        unityVersion: $("unityProfileUnityVersion").value || null,
        unityExecutablePath: $("unityProfileUnityExe").value || null,
        unityBuildMethod: $("unityProfileBuildMethod").value || null,
        productName: $("unityProfileProductName").value || null,
        bundleIdentifier: $("unityProfileBundleId").value || null,
      }),
    });
    resetUnityProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage(isEditing ? "工程模板已更新。" : "工程模板已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("unityProfileSaveBtn", false);
  }
}

function handleUnityProfilesListClick(event) {
  if (!event.target.closest) return;
  const editBtn = event.target.closest("[data-edit-up-id]");
  if (editBtn) {
    editUnityProfile(editBtn.dataset.editUpId);
    return;
  }
  const deleteBtn = event.target.closest("[data-delete-up-id]");
  if (deleteBtn) {
    deleteUnityProfile(deleteBtn.dataset.deleteUpId);
  }
}

function editUnityProfile(profileId) {
  const profile = state.unityProjectProfiles.find((u) => u.id === profileId);
  if (!profile) return;
  $("unityProfileId").value = profile.id;
  $("unityProfileName").value = profile.name || "";
  $("unityProfileUnityPath").value = profile.unityProjectRelativePath || ".";
  $("unityProfileUnityVersion").value = profile.unityVersion || "";
  $("unityProfileUnityExe").value = profile.unityExecutablePath || "";
  $("unityProfileBuildMethod").value = profile.unityBuildMethod || "";
  $("unityProfileProductName").value = profile.productName || "";
  $("unityProfileBundleId").value = profile.bundleIdentifier || "";
  $("unityProfileFormTitle").textContent = "编辑工程模板";
  $("unityProfileSaveBtn").textContent = "更新模板";
  $("unityProfileCancelBtn").classList.remove("hidden");
  $("unityProfileForm").scrollIntoView({ behavior: "smooth", block: "start" });
}

function resetUnityProfileForm() {
  $("unityProfileForm").reset();
  $("unityProfileId").value = "";
  $("unityProfileUnityPath").value = ".";
  $("unityProfileFormTitle").textContent = "新增工程模板";
  $("unityProfileSaveBtn").textContent = "保存模板";
  $("unityProfileCancelBtn").classList.add("hidden");
}

async function deleteUnityProfile(profileId) {
  if (!confirm("确认删除这个工程模板？")) return;
  try {
    await api(`/api/unity-project-profiles/${encodeURIComponent(profileId)}`, { method: "DELETE" });
    if ($("unityProfileId").value === profileId) resetUnityProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage("工程模板已删除。");
  } catch (error) {
    showError(error);
  }
}

function renderUnityProfiles() {
  if (state.unityProjectProfiles.length === 0) {
    $("unityProfilesList").innerHTML = `<div class="empty-state compact">暂无工程模板。使用左侧表单添加。</div>`;
    return;
  }

  $("unityProfilesList").innerHTML = state.unityProjectProfiles.map((u) => `<article class="item">
    <header>
      <div>
        <strong>${escapeHtml(u.name)}</strong>
      </div>
      <div class="item-actions">
        <button class="secondary" type="button" data-edit-up-id="${escapeHtml(u.id)}">编辑</button>
        <button class="danger" type="button" data-delete-up-id="${escapeHtml(u.id)}">删除</button>
      </div>
    </header>
    <dl class="project-meta">
      <div><dt>Unity 版本</dt><dd>${escapeHtml(u.unityVersion || "-")}</dd></div>
      <div><dt>Product Name</dt><dd>${escapeHtml(u.productName || "-")}</dd></div>
      <div><dt>Bundle ID</dt><dd>${escapeHtml(u.bundleIdentifier || "-")}</dd></div>
    </dl>
  </article>`).join("");
}

// ---- Certificate Profiles ----

function toggleCertProfilePlatformFields() {
  const platform = $("certProfilePlatform").value;
  const showAll = platform === "all";
  $("certProfileIosFields").classList.toggle("hidden", !showAll && platform !== "ios");
  $("certProfileAndroidFields").classList.toggle("hidden", !showAll && platform !== "android");
  $("certProfileTiktokFields").classList.toggle("hidden", !showAll && platform !== "tiktok");
}

async function saveCertProfile(event) {
  event.preventDefault();
  clearMessage();
  const profileId = $("certProfileId").value;
  const isEditing = Boolean(profileId);
  setButtonBusy("certProfileSaveBtn", true, isEditing ? "更新中..." : "保存中...");
  try {
    const path = isEditing ? `/api/certificate-profiles/${encodeURIComponent(profileId)}` : "/api/certificate-profiles";
    const method = isEditing ? "PUT" : "POST";
    await api(path, {
      method,
      body: JSON.stringify({
        name: $("certProfileName").value,
        platform: $("certProfilePlatform").value,
        appStoreConnectApiKeyPath: $("certProfileApiKeyPath").value || null,
        appStoreConnectApiKeyId: $("certProfileApiKeyId").value || null,
        appStoreConnectApiIssuerId: $("certProfileIssuerId").value || null,
        appStoreConnectUploadEnabled: $("certProfileAscUpload").checked,
        googlePlayUploadEnabled: $("certProfileGpUpload").checked,
        googlePlayPackageName: $("certProfileGpPackage").value || null,
        googlePlayServiceAccountJsonPath: $("certProfileGpServiceJson").value || null,
        googlePlayTrack: $("certProfileGpTrack").value,
        tiktokAppId: $("certProfileTiktokAppId").value || null,
        tiktokAccessToken: $("certProfileTiktokToken").value || null,
        tiktokGameName: $("certProfileTiktokGameName").value || null,
        tiktokApiEndpoint: $("certProfileTiktokEndpoint").value || "https://open-api.tiktokglobalshop.com",
        tiktokUploadEnabled: $("certProfileTiktokUpload").checked,
      }),
    });
    resetCertProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage(isEditing ? "证书模板已更新。" : "证书模板已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("certProfileSaveBtn", false);
  }
}

function handleCertProfilesListClick(event) {
  if (!event.target.closest) return;
  const editBtn = event.target.closest("[data-edit-cp-id]");
  if (editBtn) {
    editCertProfile(editBtn.dataset.editCpId);
    return;
  }
  const deleteBtn = event.target.closest("[data-delete-cp-id]");
  if (deleteBtn) {
    deleteCertProfile(deleteBtn.dataset.deleteCpId);
  }
}

function editCertProfile(profileId) {
  const profile = state.certificateProfiles.find((c) => c.id === profileId);
  if (!profile) return;
  $("certProfileId").value = profile.id;
  $("certProfileName").value = profile.name || "";
  $("certProfilePlatform").value = profile.platform || "ios";
  $("certProfileApiKeyPath").value = profile.appStoreConnectApiKeyPath || "";
  $("certProfileApiKeyId").value = profile.appStoreConnectApiKeyId || "";
  $("certProfileIssuerId").value = profile.appStoreConnectApiIssuerId || "";
  $("certProfileAscUpload").checked = Boolean(profile.appStoreConnectUploadEnabled);
  $("certProfileGpPackage").value = profile.googlePlayPackageName || "";
  $("certProfileGpServiceJson").value = profile.googlePlayServiceAccountJsonPath || "";
  $("certProfileGpTrack").value = profile.googlePlayTrack || "internal";
  $("certProfileGpUpload").checked = Boolean(profile.googlePlayUploadEnabled);
  $("certProfileTiktokAppId").value = profile.tiktokAppId || "";
  $("certProfileTiktokToken").value = profile.tiktokAccessToken || "";
  $("certProfileTiktokGameName").value = profile.tiktokGameName || "";
  $("certProfileTiktokEndpoint").value = profile.tiktokApiEndpoint || "https://open-api.tiktokglobalshop.com";
  $("certProfileTiktokUpload").checked = Boolean(profile.tiktokUploadEnabled);
  $("certProfileFormTitle").textContent = "编辑证书模板";
  $("certProfileSaveBtn").textContent = "更新模板";
  $("certProfileCancelBtn").classList.remove("hidden");
  toggleCertProfilePlatformFields();
  $("certProfileForm").scrollIntoView({ behavior: "smooth", block: "start" });
}

function resetCertProfileForm() {
  $("certProfileForm").reset();
  $("certProfileId").value = "";
  $("certProfilePlatform").value = "ios";
  $("certProfileGpTrack").value = "internal";
  $("certProfileTiktokEndpoint").value = "https://open-api.tiktokglobalshop.com";
  $("certProfileFormTitle").textContent = "新增证书模板";
  $("certProfileSaveBtn").textContent = "保存模板";
  $("certProfileCancelBtn").classList.add("hidden");
  toggleCertProfilePlatformFields();
}

async function deleteCertProfile(profileId) {
  if (!confirm("确认删除这个证书模板？")) return;
  try {
    await api(`/api/certificate-profiles/${encodeURIComponent(profileId)}`, { method: "DELETE" });
    if ($("certProfileId").value === profileId) resetCertProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage("证书模板已删除。");
  } catch (error) {
    showError(error);
  }
}

function renderCertProfiles() {
  if (state.certificateProfiles.length === 0) {
    $("certProfilesList").innerHTML = `<div class="empty-state compact">暂无证书模板。使用左侧表单添加。</div>`;
    return;
  }

  $("certProfilesList").innerHTML = state.certificateProfiles.map((c) => `<article class="item">
    <header>
      <div>
        <strong>${escapeHtml(c.name)}</strong>
        <div class="muted small">${platformLabel(c.platform || "ios")}</div>
      </div>
      <div class="item-actions">
        <button class="secondary" type="button" data-edit-cp-id="${escapeHtml(c.id)}">编辑</button>
        <button class="danger" type="button" data-delete-cp-id="${escapeHtml(c.id)}">删除</button>
      </div>
    </header>
    <dl class="project-meta">
      <div><dt>ASC 上传</dt><dd>${c.appStoreConnectUploadEnabled ? "启用" : "-"}</dd></div>
      <div><dt>Google Play</dt><dd>${c.googlePlayUploadEnabled ? "启用" : "-"}</dd></div>
      <div><dt>TikTok App ID</dt><dd>${escapeHtml(c.tiktokAppId || "-")}</dd></div>
    </dl>
  </article>`).join("");
}

// ---- Signing Profiles ----

function toggleSigningProfilePlatformFields() {
  const platform = $("signingProfilePlatform").value;
  const showAll = platform === "all";
  $("signingProfileIosFields").classList.toggle("hidden", !showAll && platform !== "ios");
  $("signingProfileAndroidFields").classList.toggle("hidden", !showAll && platform !== "android");
}

async function saveSigningProfile(event) {
  event.preventDefault();
  clearMessage();
  const profileId = $("signingProfileId").value;
  const isEditing = Boolean(profileId);
  setButtonBusy("signingProfileSaveBtn", true, isEditing ? "更新中..." : "保存中...");
  try {
    const path = isEditing ? `/api/signing-profiles/${encodeURIComponent(profileId)}` : "/api/signing-profiles";
    const method = isEditing ? "PUT" : "POST";
    await api(path, {
      method,
      body: JSON.stringify({
        name: $("signingProfileName").value,
        platform: $("signingProfilePlatform").value,
        teamId: $("signingProfileTeamId").value || null,
        exportMethod: $("signingProfileExportMethod").value,
        signingStyle: $("signingProfileSigningStyle").value,
        iosDeploymentTarget: $("signingProfileIosTarget").value || null,
        androidKeystoreName: $("signingProfileKeystoreName").value || null,
        androidKeystorePass: $("signingProfileKeystorePass").value || null,
        androidKeyaliasName: $("signingProfileKeyaliasName").value || null,
        androidKeyaliasPass: $("signingProfileKeyaliasPass").value || null,
      }),
    });
    resetSigningProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage(isEditing ? "签名模板已更新。" : "签名模板已保存。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("signingProfileSaveBtn", false);
  }
}

function handleSigningProfilesListClick(event) {
  if (!event.target.closest) return;
  const editBtn = event.target.closest("[data-edit-sp-id]");
  if (editBtn) {
    editSigningProfile(editBtn.dataset.editSpId);
    return;
  }
  const deleteBtn = event.target.closest("[data-delete-sp-id]");
  if (deleteBtn) {
    deleteSigningProfile(deleteBtn.dataset.deleteSpId);
  }
}

function editSigningProfile(profileId) {
  const profile = state.signingProfiles.find((s) => s.id === profileId);
  if (!profile) return;
  $("signingProfileId").value = profile.id;
  $("signingProfileName").value = profile.name || "";
  $("signingProfilePlatform").value = profile.platform || "ios";
  $("signingProfileTeamId").value = profile.teamId || "";
  $("signingProfileExportMethod").value = profile.exportMethod || "development";
  $("signingProfileSigningStyle").value = profile.signingStyle || "automatic";
  $("signingProfileIosTarget").value = profile.iosDeploymentTarget || "";
  $("signingProfileKeystoreName").value = profile.androidKeystoreName || "";
  $("signingProfileKeystorePass").value = profile.androidKeystorePass || "";
  $("signingProfileKeyaliasName").value = profile.androidKeyaliasName || "";
  $("signingProfileKeyaliasPass").value = profile.androidKeyaliasPass || "";
  $("signingProfileFormTitle").textContent = "编辑签名模板";
  $("signingProfileSaveBtn").textContent = "更新模板";
  $("signingProfileCancelBtn").classList.remove("hidden");
  toggleSigningProfilePlatformFields();
  $("signingProfileForm").scrollIntoView({ behavior: "smooth", block: "start" });
}

function resetSigningProfileForm() {
  $("signingProfileForm").reset();
  $("signingProfileId").value = "";
  $("signingProfilePlatform").value = "ios";
  $("signingProfileExportMethod").value = "development";
  $("signingProfileSigningStyle").value = "automatic";
  $("signingProfileFormTitle").textContent = "新增签名模板";
  $("signingProfileSaveBtn").textContent = "保存模板";
  $("signingProfileCancelBtn").classList.add("hidden");
  toggleSigningProfilePlatformFields();
}

async function deleteSigningProfile(profileId) {
  if (!confirm("确认删除这个签名模板？")) return;
  try {
    await api(`/api/signing-profiles/${encodeURIComponent(profileId)}`, { method: "DELETE" });
    if ($("signingProfileId").value === profileId) resetSigningProfileForm();
    await refreshAll({ showSuccess: false, throwOnError: true });
    showMessage("签名模板已删除。");
  } catch (error) {
    showError(error);
  }
}

function renderSigningProfiles() {
  if (state.signingProfiles.length === 0) {
    $("signingProfilesList").innerHTML = `<div class="empty-state compact">暂无签名模板。使用左侧表单添加。</div>`;
    return;
  }

  $("signingProfilesList").innerHTML = state.signingProfiles.map((s) => `<article class="item">
    <header>
      <div>
        <strong>${escapeHtml(s.name)}</strong>
        <div class="muted small">${platformLabel(s.platform || "ios")}</div>
      </div>
      <div class="item-actions">
        <button class="secondary" type="button" data-edit-sp-id="${escapeHtml(s.id)}">编辑</button>
        <button class="danger" type="button" data-delete-sp-id="${escapeHtml(s.id)}">删除</button>
      </div>
    </header>
    <dl class="project-meta">
      <div><dt>Team ID</dt><dd>${escapeHtml(s.teamId || "-")}</dd></div>
      <div><dt>Export Method</dt><dd>${escapeHtml(s.exportMethod || "-")}</dd></div>
      <div><dt>Keystore</dt><dd>${escapeHtml(s.androidKeystoreName || "-")}</dd></div>
    </dl>
  </article>`).join("");
}

// ---- Config File Browser ----

async function openConfigFileBrowser() {
  const modal = $("configFileBrowserModal");
  const listEl = $("configFileBrowserList");
  listEl.innerHTML = loadingItem("正在读取配置文件列表...");
  modal.classList.remove("hidden");
  modal.setAttribute("aria-hidden", "false");

  try {
    const files = await api("/api/config-files/list");
    if (!files.length) {
      listEl.innerHTML = `<div class="empty-state compact">配置目录下暂无 JSON 文件。</div>`;
      return;
    }

    listEl.innerHTML = files.map((f) => `<article class="item" style="cursor: pointer;" data-config-file-path="${escapeHtml(f.path)}">
      <header>
        <div>
          <strong>${escapeHtml(f.name)}</strong>
          <div class="muted small">${escapeHtml(f.path)}</div>
        </div>
        <button class="secondary" type="button">选择</button>
      </header>
    </article>`).join("");
  } catch (error) {
    listEl.innerHTML = `<div class="empty-state compact">读取失败：${escapeHtml(error.message || error)}</div>`;
  }
}

function closeConfigFileBrowser() {
  const modal = $("configFileBrowserModal");
  modal.classList.add("hidden");
  modal.setAttribute("aria-hidden", "true");
}

// ---- Data Manager ----

function toggleExportAll() {
  const checkboxes = document.querySelectorAll(".export-cat");
  const allChecked = Array.from(checkboxes).every((cb) => cb.checked);
  checkboxes.forEach((cb) => { cb.checked = !allChecked; });
}

async function exportData() {
  const categories = Array.from(document.querySelectorAll(".export-cat:checked")).map((cb) => cb.value);
  if (categories.length === 0) {
    showError(new Error("请至少选择一个数据类别。"));
    return;
  }

  setButtonBusy("exportBtn", true, "导出中...");
  try {
    const data = await api("/api/data/export", {
      method: "POST",
      body: JSON.stringify(categories),
    });
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `buildserver-data-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    showMessage("数据已导出。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("exportBtn", false);
  }
}

async function importData() {
  const fileInput = $("importFileInput");
  const file = fileInput.files?.[0];
  if (!file) {
    showError(new Error("请先选择要导入的 JSON 文件。"));
    return;
  }

  if (!confirm("导入会按 ID 去重，已存在的记录会被跳过。确认导入？")) return;

  setButtonBusy("importBtn", true, "导入中...");
  const resultEl = $("importResult");
  resultEl.classList.remove("hidden");
  resultEl.className = "toast";
  resultEl.textContent = "正在导入...";
  try {
    const text = await file.text();
    JSON.parse(text);
    const result = await api("/api/data/import", {
      method: "POST",
      body: text,
    });
    resultEl.className = "toast";
    resultEl.textContent = `导入完成，共导入 ${result.imported} 条记录。`;
    await refreshAll({ showSuccess: false });
    fileInput.value = "";
  } catch (error) {
    resultEl.className = "toast error";
    resultEl.textContent = `导入失败：${error.message || error}`;
    showError(error);
  } finally {
    setButtonBusy("importBtn", false);
  }
}

init();
