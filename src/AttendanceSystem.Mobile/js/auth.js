function readLoginValue(id) {
  const el = document.getElementById(id);
  return el ? el.value.trim() : "";
}

function decodeJwt(token) {
  try {
    if (!token) return null;
    const payload = token.split(".")[1];
    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const json = decodeURIComponent(
      atob(normalized)
        .split("")
        .map(c => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
        .join("")
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function getCurrentUserName() {
  const payload = decodeJwt(getAccessToken());
  return (
    payload?.fullName ||
    payload?.name ||
    payload?.unique_name ||
    payload?.email ||
    "Б.Батаа"
  );
}

function getCurrentDepartmentId() {
  const payload = decodeJwt(getAccessToken());
  return payload?.department_id || payload?.departmentId || "Тодорхойгүй";
}

async function parseJsonResponse(res) {
  const text = await res.text();
  if (!text) return null;

  try {
    return JSON.parse(text);
  } catch {
    throw new Error("Серверээс ирсэн өгөгдлийг уншиж чадсангүй.");
  }
}

async function doLogin(email, password) {
  const loginEmail = email ?? readLoginValue("login-email");
  const loginPassword = password ?? readLoginValue("login-pass");

  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      email: loginEmail,
      password: loginPassword
    })
  });

  if (!res.ok) {
    const errorData = await parseResponseText(res);
    const message = errorData?.message || errorData || "Нэвтрэх амжилтгүй";
    throw new Error(message);
  }

  const data = await parseJsonResponse(res);
  if (!data || !data.accessToken) {
    throw new Error("Серверээс хүчин төгөлдөр тасалбар ирсэнгүй.");
  }

  setTokens(data.accessToken, data.refreshToken);

  if (data.employeeId) {
    localStorage.setItem("employeeId", data.employeeId);
  }

  return data;
}

async function refreshAccessToken() {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return false;

  const res = await fetch(`${API_BASE}/api/auth/refresh`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      refreshToken
    })
  });

  if (!res.ok) return false;

  const data = await parseJsonResponse(res);
  if (!data || !data.accessToken) return false;
  setTokens(data.accessToken, data.refreshToken);

  return true;
}

function doLogout() {
  clearTokens();
  localStorage.removeItem("employeeId");
  window.location.href = "login.html";
}