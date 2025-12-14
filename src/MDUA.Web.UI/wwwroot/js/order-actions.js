// =========================================================
// 1. GLOBAL SCOPE FUNCTIONS
// =========================================================
window.openAdvanceModal = function (element) {
    // 1. EXTRACT DATA
    const btn = element;
    const orderRef = btn.getAttribute('data-order-ref');
    const custId = btn.getAttribute('data-cust-id');

    // Note: 'data-net' here comes from DB Total Amount (which includes delivery)
    const totalPayable = parseFloat(btn.getAttribute('data-net')) || 0;
    const alreadyPaid = parseFloat(btn.getAttribute('data-paid')) || 0;

    // Delivery is just for reference/display
    let storedDelivery = parseFloat(btn.getAttribute('data-delivery')) || 0;

    // 2. POPULATE INPUTS
    document.getElementById('adv_orderRef').value = orderRef;
    document.getElementById('adv_customerId').value = custId;

    // Fill the Read-Only Total Field
    document.getElementById('adv_netAmount').value = totalPayable;

    // Fill History
    document.getElementById('adv_alreadyPaid').value = alreadyPaid;
    document.getElementById('adv_displayPaid').textContent = alreadyPaid.toFixed(2);

    // Fill Delivery (Editable)
    document.getElementById('adv_delivery').value = storedDelivery;

    // Reset User Fields
    document.getElementById('adv_paidAmount').value = "";
    document.getElementById('adv_note').value = "";

    // Reset Dropdown
    const typeSelect = document.getElementById('adv_paymentType');
    if (typeSelect) typeSelect.value = "Advance";

    // Clear Errors
    const paidInput = document.getElementById('adv_paidAmount');
    const submitBtn = document.getElementById('adv_submitBtn');
    if (paidInput) paidInput.classList.remove('is-invalid');
    if (submitBtn) submitBtn.disabled = false;

    // =========================================================
    // ✅ CRITICAL FIX: IMMEDIATE CALCULATION
    // =========================================================
    // We calculate this NOW, we don't wait for an event listener.

    // 1. Current Due = Total Payable - Already Paid
    const currentDue = totalPayable - alreadyPaid;

    // 2. Balance After = Current Due - 0 (since input is empty)
    const balanceAfter = currentDue;

    // 3. Update UI Text Immediately
    const dueDisplay = document.getElementById('adv_displayCurrentDue');
    const balanceDisplay = document.getElementById('adv_dueAmount');

    if (dueDisplay) {
        if (currentDue <= 0) {
            dueDisplay.innerHTML = '<span class="text-success"><i class="fas fa-check"></i> Fully Paid</span>';
        } else {
            dueDisplay.textContent = currentDue.toFixed(2);
        }
    }

    if (balanceDisplay) {
        balanceDisplay.textContent = balanceAfter.toFixed(2);
    }
    // =========================================================

    // Move & Show Modal
    const modalEl = document.getElementById('advanceModal');
    if (modalEl.parentElement !== document.body) {
        document.body.appendChild(modalEl);
    }
    const modal = new bootstrap.Modal(modalEl);
    modal.show();
};

// =========================================================
// 2. DOM LOADED EVENTS
// =========================================================
document.addEventListener('DOMContentLoaded', function () {

    // --- A. TOGGLE CONFIRMATION ---
    const toggles = document.querySelectorAll('.confirm-toggle');
    toggles.forEach(toggle => {
        toggle.addEventListener('change', function () {
            const checkbox = this;
            const orderId = checkbox.getAttribute('data-id');
            const isConfirmed = checkbox.checked;
            const statusBadge = document.getElementById(`status-badge-${orderId}`);
            const label = checkbox.nextElementSibling;

            if (label) label.textContent = isConfirmed ? "Yes" : "No";

            const formData = new URLSearchParams();
            formData.append('id', orderId);
            formData.append('isConfirmed', isConfirmed);
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

            fetch('/SalesOrder/ToggleConfirmation', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: formData.toString()
            }).then(r => r.json()).then(data => {
                if (data.success && statusBadge) {
                    statusBadge.textContent = data.newStatus;
                    statusBadge.className = data.newStatus === 'Confirmed' ? 'badge bg-success text-white' : 'badge bg-warning text-dark';
                } else if (!data.success) {
                    checkbox.checked = !isConfirmed;
                    alert("Action failed: " + data.message);
                }
            }).catch(err => {
                checkbox.checked = !isConfirmed;
                alert("Network error.");
            });
        });
    });

    // --- B. AUTO-CALCULATION & VALIDATION ---
    const netInput = document.getElementById('adv_netAmount');
    const alreadyPaidInput = document.getElementById('adv_alreadyPaid');
    const payInput = document.getElementById('adv_paidAmount');
    const submitBtn = document.getElementById('adv_submitBtn');

    const displayCurrentDue = document.getElementById('adv_displayCurrentDue');
    const displayBalance = document.getElementById('adv_dueAmount');
    const typeSelect = document.getElementById('adv_paymentType');

    function calculateTotals() {
        // Get values
        const totalPayable = parseFloat(netInput.value) || 0;
        const alreadyPaid = parseFloat(alreadyPaidInput.value) || 0;
        const payingNow = parseFloat(payInput.value) || 0;

        // 1. Calculate Current Due
        const currentDue = totalPayable - alreadyPaid;

        // 2. Calculate Balance After
        const balanceAfter = currentDue - payingNow;

        // 3. Update Display
        if (displayCurrentDue) {
            if (currentDue <= 0) {
                displayCurrentDue.innerHTML = '<span class="text-success"><i class="fas fa-check"></i> Fully Paid</span>';
            } else {
                displayCurrentDue.textContent = currentDue.toFixed(2);
            }
        }

        if (displayBalance) {
            displayBalance.textContent = balanceAfter.toFixed(2);
            // Visual coloring for negative balance (overpayment/tip)
            if (balanceAfter < 0) {
                displayBalance.parentElement.className = "text-success fw-bold fs-5";
            } else {
                displayBalance.parentElement.className = "text-danger fw-bold fs-5";
            }
        }

        // 4. Validation (Prevent Paying > Due)
        // Allow small buffer (0.01) for rounding issues
        if (payingNow > (currentDue + 0.01) && currentDue > 0) {
            payInput.classList.add('is-invalid');
            if (submitBtn) submitBtn.disabled = true;
        } else {
            payInput.classList.remove('is-invalid');
            if (submitBtn) submitBtn.disabled = false;
        }

        return { currentDue, payingNow };
    }

    // Input Listener
    if (payInput) {
        payInput.addEventListener('input', function () {
            const { currentDue, payingNow } = calculateTotals();

            // Auto-switch dropdown
            if (typeSelect && payingNow <= currentDue) {
                if (payingNow >= currentDue && currentDue > 0) typeSelect.value = "Sale";
                else typeSelect.value = "Advance";
            }
        });
    }

    // Dropdown Listener
    if (typeSelect) {
        typeSelect.addEventListener('change', function () {
            const { currentDue } = calculateTotals();

            if (this.value === 'Sale') {
                // Auto-fill remaining due
                payInput.value = currentDue > 0 ? currentDue : 0;
            } else {
                // Clear input
                payInput.value = "";
            }
            // Trigger recalculation to update balance/validation
            calculateTotals();
        });
    }

    // --- C. SUBMIT FORM ---
    const advanceForm = document.getElementById('advanceForm');
    if (advanceForm) {
        advanceForm.addEventListener('submit', function (e) {
            e.preventDefault();

            // Note: We send DeliveryCharge just for updating DB if backend supports it, 
            // otherwise it's ignored.
            const deliveryVal = parseFloat(document.getElementById('adv_delivery').value) || 0;

            const payload = {
                CustomerId: parseInt(document.getElementById('adv_customerId').value),
                PaymentMethodId: parseInt(document.getElementById('adv_paymentMethod').value),
                PaymentType: document.getElementById('adv_paymentType').value,
                Amount: parseFloat(document.getElementById('adv_paidAmount').value),
                TransactionReference: document.getElementById('adv_orderRef').value,
                Notes: document.getElementById('adv_note').value,
                // Optional: pass delivery if you have an update logic
                // DeliveryCharge: deliveryVal 
            };

            fetch('/order/add-payment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            }).then(r => r.json()).then(data => {
                if (data.success) {
                    alert("Payment Added Successfully!");
                    location.reload();
                } else {
                    alert("Error: " + data.message);
                }
            }).catch(err => alert("Network Error"));
        });
    }
});