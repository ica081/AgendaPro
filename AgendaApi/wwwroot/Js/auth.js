const API_URL = "http://localhost:5182";

async function register() {
    const email = document.getElementById("email").value;
    const password = document.getElementById("password").value;
    // Agora sempre será "Company" (fixo, pois clientes não se cadastram mais)
    const userType = "Company";

    const response = await fetch(`${API_URL}/auth/index`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, type: userType })
    });

    if (!response.ok) {
        const error = await response.text();
        alert("Erro ao cadastrar: " + error);
        return;
    }

    alert("Conta criada com sucesso!");
    window.location.href = "/shared/login.html";
}

function togglePassword() {
    const input = document.getElementById("password");
    input.type = input.type === "password" ? "text" : "password";
}

async function login() {
    const email = document.getElementById("email").value;
    const password = document.getElementById("password").value;

    const response = await fetch(`${API_URL}/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password })
    });

    if (!response.ok) {
        alert("Login inválido");
        return;
    }

    const data = await response.json();
    localStorage.setItem("token", data.token);
    localStorage.setItem("companyId", data.companyId || "");
    localStorage.setItem("userType", data.userType);

    // 🔥 Sempre redirecionar para o dashboard da empresa
    window.location.href = "/company/dashboard-empresa.html";
}