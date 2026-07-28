const API_URL = "http://localhost:5182";

async function register() {
    const email = document.getElementById("email").value;
    const password = document.getElementById("password").value;
    const userType = document.querySelector('input[name="userType"]:checked').value;

    const response = await fetch(`${API_URL}/auth/register`, {
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

    if (data.userType === "Company") {
        window.location.href = "/company/dashboard-empresa.html";
    } else {
        window.location.href = "/client/dashboard-cliente.html";
    }
}