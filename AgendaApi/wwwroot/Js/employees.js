const API_URL = "https://agendaapi-4772.onrender.com";

function getToken() {
    return localStorage.getItem("token");
}

async function loadEmployees() {
    const token = getToken();
    if (!token) {
        alert("Token não encontrado. Faça login.");
        window.location.href = "login.html";
        return;
    }

    try {
        const response = await fetch(`${API_URL}/employees`, {
            headers: { "Authorization": "Bearer " + token }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        const data = await response.json();
        const list = document.getElementById("employee-list");
        list.innerHTML = "";

        if (!Array.isArray(data) || data.length === 0) {
            list.innerHTML = "<p>Nenhum funcionário cadastrado.</p>";
            return;
        }

        data.forEach(emp => {
            const div = document.createElement("div");
            div.className = "employee-item";

            const photoHtml = emp.photoUrl
                ? `<img src="${emp.photoUrl}" alt="Foto">`
                : `<div style="width:50px;height:50px;border-radius:50%;background:#ddd;display:flex;align-items:center;justify-content:center;">📸</div>`;

            div.innerHTML = `
                ${photoHtml}
                <div style="flex:1;">
                    <strong>${emp.name}</strong><br>
                    <small>${emp.specialty || 'Sem especialidade'}</small>
                </div>
                <div class="actions">
                    <button class="edit-btn" onclick="editEmployee('${emp.id}')">Editar</button>
                    <button class="delete-btn" onclick="deleteEmployee('${emp.id}')">Excluir</button>
                </div>
            `;
            list.appendChild(div);
        });
    } catch (error) {
        console.error("Erro ao carregar funcionários:", error);
        alert("Erro ao carregar funcionários: " + error.message);
    }
}

async function saveEmployee() {
    const id = document.getElementById("edit-id").value;
    const name = document.getElementById("name").value.trim();
    const specialty = document.getElementById("specialty").value.trim();
    const photoUrl = document.getElementById("photoUrl").value.trim();

    if (!name) {
        alert("O nome é obrigatório.");
        return;
    }

    const method = id ? "PUT" : "POST";
    const url = id ? `${API_URL}/employees/${id}` : `${API_URL}/employees`;
    const body = { name, specialty, photoUrl };

    const token = getToken();
    if (!token) {
        alert("Token não encontrado. Faça login.");
        return;
    }

    try {
        const response = await fetch(url, {
            method: method,
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

        alert(id ? "Funcionário atualizado!" : "Funcionário criado!");
        cancelEdit();
        loadEmployees();
    } catch (error) {
        console.error("Erro ao salvar:", error);
        alert("Erro ao salvar: " + error.message);
    }
}

function editEmployee(id) {
    // Busca o funcionário na lista atual ou faz uma requisição
    // Vamos fazer uma requisição para obter os dados específicos
    const token = getToken();
    fetch(`${API_URL}/employees/${id}`, {
        headers: { "Authorization": "Bearer " + token }
    })
    .then(response => {
        if (!response.ok) throw new Error("Erro ao buscar dados");
        return response.json();
    })
    .then(emp => {
        document.getElementById("edit-id").value = emp.id;
        document.getElementById("name").value = emp.name;
        document.getElementById("specialty").value = emp.specialty || "";
        document.getElementById("photoUrl").value = emp.photoUrl || "";
        document.getElementById("form-title").textContent = "Editar Funcionário";
        document.getElementById("save-btn").textContent = "Atualizar";
    })
    .catch(error => {
        alert("Erro ao carregar dados do funcionário: " + error.message);
    });
}

function cancelEdit() {
    document.getElementById("edit-id").value = "";
    document.getElementById("name").value = "";
    document.getElementById("specialty").value = "";
    document.getElementById("photoUrl").value = "";
    document.getElementById("form-title").textContent = "Novo Funcionário";
    document.getElementById("save-btn").textContent = "Salvar";
}

async function deleteEmployee(id) {
    if (!confirm("Tem certeza que deseja excluir este funcionário?")) return;

    const token = getToken();
    try {
        const response = await fetch(`${API_URL}/employees/${id}`, {
            method: "DELETE",
            headers: { "Authorization": "Bearer " + token }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        alert("Funcionário excluído!");
        loadEmployees();
    } catch (error) {
        console.error("Erro ao excluir:", error);
        alert("Erro ao excluir: " + error.message);
    }
}

window.onload = loadEmployees;