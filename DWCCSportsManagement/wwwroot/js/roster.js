function selectSport (event, sportName) {
    event.preventDefault();

    console.log("Selected sport:", sportName);

    // Update dropdown text
    const dropdownBtn = document.getElementById("sportDropdownBtn");
    dropdownBtn.textContent = sportName === "all" ? "All Sports" : sportName;

    // Get all athlete rows
    const rows = document.querySelectorAll("#athleteTable tbody tr");

    // Filter rows by selected sport
    rows.forEach(row => {
        const athleteSport = row.getAttribute("data-sport");
        if (sportName === "all" || athleteSport === sportName) {
            row.style.display = "";
        } else {
            row.style.display = "none";
        }
    });
};


const saveBtn = document.querySelector('.add-session-save-btn');
const tableBody = document.getElementById('sessionsTableBody');
let sessionCounter = tableBody.rows.length;
let editingRow = null; // store the row being edited

// Save or update session
saveBtn.addEventListener('click', () => {
    const dateTime = document.getElementById('sessionDateTime').value;
    const teamName = document.getElementById('teamName').value;
    const description = document.getElementById('description').value;
    const location = document.getElementById('location').value;

    if (!dateTime || !teamName || !description || !location) {
        alert('Please fill in all fields.');
        return;
    }

    if (editingRow) { // Editing existing row
        editingRow.cells[1].textContent = new Date(dateTime).toLocaleString();
        editingRow.cells[2].textContent = teamName;
        editingRow.cells[3].textContent = description;
        editingRow.cells[4].textContent = location;
        editingRow = null;
    } else { // Adding new row
        sessionCounter++;
        const newRow = document.createElement('tr');
        newRow.innerHTML = `
                <td>${sessionCounter}</td>
                <td>${new Date(dateTime).toLocaleString()}</td>
                <td>${teamName}</td>
                <td>${description}</td>
                <td>${location}</td>
                <td>
                    <button class="btn btn-sm btn-primary edit-btn">Edit</button>
                    <button class="btn btn-sm btn-danger delete-btn">Delete</button>
                </td>
            `;
        tableBody.appendChild(newRow);
    }

    document.getElementById('addSessionForm').reset();

    // Close modal
    const modalEl = document.getElementById('addSessionModal');
    const modal = bootstrap.Modal.getInstance(modalEl);
    modal.hide();
    attachActionListeners(); // reattach listeners for new row
});

// Function to attach edit/delete listeners
function attachActionListeners() {
    document.querySelectorAll('.edit-btn').forEach(btn => {
        btn.onclick = (e) => {
            const row = e.target.closest('tr');
            editingRow = row;

            document.getElementById('sessionDateTime').value = new Date(row.cells[1].textContent).toISOString().slice(0, 16);
            document.getElementById('teamName').value = row.cells[2].textContent;
            document.getElementById('description').value = row.cells[3].textContent;
            document.getElementById('location').value = row.cells[4].textContent;

            const modal = new bootstrap.Modal(document.getElementById('addSessionModal'));
            modal.show();
        };
    });

    document.querySelectorAll('.delete-btn').forEach(btn => {
        btn.onclick = (e) => {
            if (confirm('Are you sure you want to delete this session?')) {
                const row = e.target.closest('tr');
                row.remove();
            }
        };
    });
}

attachActionListeners(); // initial attach
