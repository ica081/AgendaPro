const API_URL = "http://localhost:5182";

function getToken() {
    return localStorage.getItem("token");
}

async function createCompany() {
    const name = document.getElementById("companyName").value.trim();
    const category = document.getElementById("category").value.trim();

    if (!name || !category) {
        alert("Preencha nome e categoria.");
        return;
    }

    const token = getToken();
    if (!token) {
        alert("Faça login primeiro.");
        window.location.href = "login.html";
        return;
    }

    try {
        const response = await fetch(`${API_URL}/companies`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": "Bearer " + token
            },
            body: JSON.stringify({ name, category })
        });

        if (!response.ok) {
            const errorText = await response.text();
            alert("Erro: " + response.status + " - " + errorText);
            return;
        }

        const data = await response.json();
        console.log("Resposta:", data);

        if (data.token) {
            localStorage.setItem("token", data.token);
            localStorage.setItem("companyId", data.companyId);
        } else if (data.companyId) {
            localStorage.setItem("companyId", data.companyId);
        } else {
            alert("Resposta inesperada.");
            return;
        }

        alert("Empresa criada com sucesso!");
        window.location.href = "dashboard.html";
    } catch (error) {
        console.error(error);
        alert("Erro de conexão.");
    }
}