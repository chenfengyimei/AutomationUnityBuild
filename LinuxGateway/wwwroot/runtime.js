(function () {
  const activeActions = new WeakSet();

  function createRequestId(prefix = "web") {
    const random = globalThis.crypto?.randomUUID ? globalThis.crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    return `${prefix}-${random}`.replace(/\s+/g, "-");
  }

  async function requestJson(path, options = {}) {
    const text = await requestText(path, options);
    return text ? JSON.parse(text) : null;
  }

  async function requestText(path, options = {}) {
    const timeoutMs = options.timeoutMs ?? 30000;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);
    const headers = {
      "X-Request-Id": options.requestId || createRequestId("req"),
      ...(options.headers || {}),
    };
    if (options.body && !headers["Content-Type"]) {
      headers["Content-Type"] = "application/json";
    }

    try {
      const response = await fetch(path, {
        credentials: "include",
        ...options,
        headers,
        signal: options.signal || controller.signal,
      });
      const responseText = await response.text();
      if (!response.ok) {
        throw toRequestError(response, responseText);
      }
      return responseText;
    } catch (error) {
      if (error?.name === "AbortError") {
        throw new Error("请求超时，请检查服务状态或网络连接。");
      }
      throw error;
    } finally {
      clearTimeout(timeout);
    }
  }

  function toRequestError(response, text) {
    let message = response.statusText || "请求失败";
    let code = "";
    let traceId = response.headers.get("X-Request-Id") || "";
    try {
      const data = text ? JSON.parse(text) : {};
      message = data.detail || data.error || data.message || data.title || message;
      code = data.code || "";
      traceId = data.traceId || traceId;
    } catch {
      if (text) message = text;
    }
    const error = new Error(traceId ? `${message} (trace: ${traceId})` : message);
    error.status = response.status;
    error.code = code;
    error.traceId = traceId;
    return error;
  }

  async function runAction(buttonOrId, action, options = {}) {
    const button = typeof buttonOrId === "string" ? document.getElementById(buttonOrId) : buttonOrId;
    if (button && activeActions.has(button)) return undefined;
    setButtonBusy(button, true, options.busyText);
    try {
      return await action();
    } catch (error) {
      if (options.onError) options.onError(error);
      else throw error;
      return undefined;
    } finally {
      setButtonBusy(button, false);
    }
  }

  function setButtonBusy(buttonOrId, busy, busyText = "处理中...") {
    const button = typeof buttonOrId === "string" ? document.getElementById(buttonOrId) : buttonOrId;
    if (!button) return;
    if (!button.dataset.defaultText) {
      button.dataset.defaultText = button.textContent;
    }
    if (busy) {
      activeActions.add(button);
      button.disabled = true;
      button.setAttribute("aria-busy", "true");
      button.classList.add("is-busy");
      button.textContent = busyText || button.dataset.defaultText;
    } else {
      activeActions.delete(button);
      button.disabled = false;
      button.removeAttribute("aria-busy");
      button.classList.remove("is-busy");
      button.textContent = button.dataset.defaultText;
    }
  }

  function connectEvents(options) {
    const url = options.url || "/api/events";
    const fallbackIntervalMs = options.fallbackIntervalMs || 5000;
    let eventSource = null;
    let fallbackTimer = null;
    let closed = false;
    let errors = 0;

    function stopFallback() {
      if (fallbackTimer) {
        clearInterval(fallbackTimer);
        fallbackTimer = null;
      }
    }

    function startFallback() {
      if (fallbackTimer || closed) return;
      options.onStatus?.("事件连接不可用，已切换到轮询。");
      options.onFallbackPoll?.();
      fallbackTimer = setInterval(() => options.onFallbackPoll?.(), fallbackIntervalMs);
    }

    if (!window.EventSource) {
      startFallback();
      return { close: () => { closed = true; stopFallback(); } };
    }

    eventSource = new EventSource(url, { withCredentials: true });
    eventSource.addEventListener("open", () => {
      errors = 0;
      stopFallback();
      options.onStatus?.("实时连接已建立。");
    });
    eventSource.addEventListener("dashboard", (event) => {
      errors = 0;
      options.onDashboard?.(JSON.parse(event.data));
    });
    eventSource.addEventListener("heartbeat", (event) => {
      errors = 0;
      options.onHeartbeat?.(JSON.parse(event.data));
    });
    eventSource.addEventListener("error", () => {
      errors += 1;
      options.onStatus?.("实时连接正在重连...");
      if (errors >= 3) {
        eventSource?.close();
        startFallback();
      }
    });

    return {
      close() {
        closed = true;
        eventSource?.close();
        stopFallback();
      }
    };
  }

  window.AppRuntime = {
    requestJson,
    requestText,
    runAction,
    setButtonBusy,
    connectEvents,
    createRequestId,
  };
})();
