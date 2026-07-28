const API_URL = "http://localhost:5182";

function getToken() {
    return localStorage.getItem("token");
}

async function loadServices() {
    const token = getToken();
    if (!token) {
        alert("Você não está logado.");
        window.location.href = "login.html";
        return;
    }

    try {
        const response = await fetch(`${API_URL}/services`, {
            headers: { "Authorization": "Bearer " + token }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        const data = await response.json();

        // Verifica se é um array
        if (!Array.isArray(data)) {
            throw new Error("Resposta inesperada do servidor");
        }

        const list = document.getElementById("list");
        list.innerHTML = "";

        if (data.length === 0) {
            list.innerHTML = "<li>Nenhum serviço cadastrado</li>";
            return;
        }

        data.forEach(s => {
            const li = document.createElement("li");
            li.innerHTML = `
                ${s.name} - R$ ${s.price} (${s.durationMinutes} min)
                <button onclick="deleteService('${s.id}')">Excluir</button>
            `;
            list.appendChild(li);
        });
    } catch (error) {
        console.error("Erro ao carregar serviços:", error);
        alert("Erro ao carregar serviços: " + error.message);
    }
}

async function createService() {
    const name = document.getElementById("name").value.trim();
    const price = document.getElementById("price").value.trim();
    const duration = document.getElementById("duration").value.trim();

    if (!name || !price || !duration) {
        alert("Preencha todos os campos corretamente.");
        return;
    }

    const body = {
        name: name,
        price: Number(price),
        durationMinutes: Number(duration)
    };

    const token = getToken();
    if (!token) {
        alert("Você não está logado.");
        window.location.href = "login.html";
        return;
    }

    try {
        const response = await fetch(`${API_URL}/services`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": "Bearer " + token
            },
            body: JSON.stringify(body)
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        const result = await response.json();
        console.log("Serviço criado:", result);
        alert("Serviço criado com sucesso!");
        loadServices(); // recarrega a lista

        // Limpa os campos
        document.getElementById("name").value = "";
        document.getElementById("price").value = "";
        document.getElementById("duration").value = "";
    } catch (error) {
        console.error("Erro ao criar serviço:", error);
        alert("Erro ao criar serviço: " + error.message);
    }
}

async function deleteService(id) {
    if (!confirm("Tem certeza que deseja excluir este serviço?")) return;

    try {
        const response = await fetch(`${API_URL}/services/${id}`, {
            method: "DELETE",
            headers: { "Authorization": "Bearer " + getToken() }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        alert("Serviço excluído!");
        loadServices();
    } catch (error) {
        console.error("Erro ao excluir serviço:", error);
        alert("Erro ao excluir serviço: " + error.message);
    }
}

window.onload = loadServices;