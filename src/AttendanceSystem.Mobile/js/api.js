const API_BASE = "https://localhost:7000";

function getAccessToken() {
  return localStorage.getItem("accessToken");
}

function getRefreshToken() {
  return localStorage.getItem("refreshToken");
}

function setTokens(accessToken, refreshToken) {
  if (accessToken) {
    localStorage.setItem("accessToken", accessToken);
  }
  if (refreshToken) {
    localStorage.setItem("refreshToken", refreshToken);
  }
}

function clearTokens() {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
}

function authHeaders(extraHeaders = {}) {
  const headers = new Headers(extraHeaders);
  const token = getAccessToken();

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  return headers;
}

async function parseResponseText(response) {
  const text = await response.text();
  if (!text) return null;

  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}


async function refreshAccessToken() {

    const refreshToken = getRefreshToken();

    if (!refreshToken)
        return false;

    const response = await fetch(
        `${API_BASE}/api/v1/auth/refresh`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                refreshToken
            })
        });

    if (!response.ok)
        return false;

    const data = await response.json();

    setTokens(
        data.accessToken,
        data.refreshToken
    );

    return true;
}

async function apiFetch(path, options = {}) {
  const headers = authHeaders(options.headers || {});
  let response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers
  });

  if (response.status === 401) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      const retryHeaders = authHeaders(options.headers || {});
      response = await fetch(`${API_BASE}${path}`, {
        ...options,
        headers: retryHeaders
      });
    } else {
      clearTokens();
      window.location.href = "login.html";
      return response;
    }
  }

  return response;
}