// =========================================================
// 1. GLOBAL SCOPE VARIABLES & FUNCTIONS
// =========================================================

// Store the pure product price (Total - Old Delivery) globally to avoid DOM read issues
window.currentBasePrice = 0;

window.openAdvanceModal = function (element) {
    // --- 1. EXTRACT DATA ---
    const btn = element;
    const orderRef = btn.getAttribute('data-order-ref');
    const custId = btn.getAttribute('data-cust-id');

    // Helper: Safely parse "1,250.00" or "1250" to float
    const parseVal = (val) => {
        if (!val) return 0;
        // Remove commas if present
        const clean = val.toString().replace(/,/g, '');
        return parseFloat(clean) || 0;
    };

    // Note: 'data-net' from DB includes the OLD delivery charge
    const totalPayable = parseVal(btn.getAttribute('data-net'));
    const alreadyPaid = parseVal(btn.getAttribute('data-paid'));
    const storedDelivery = parseVal(btn.getAttribute('data-delivery'));

    // ✅ NEW: Read Product Price & Discount
    const productPrice = parseVal(btn.getAttribute('data-product-price'));
    const discount = parseVal(btn.getAttribute('data-discount'));

    // --- 2. CALCULATE BASE PRICE (CRITICAL STEP) ---
    // This is the price of items only (e.g., 1673 - 123 = 1550)
    // Logic kept unchanged as requested
    window.currentBasePrice = totalPayable - storedDelivery;

    console.log("Modal Debug:", {
        totalFromDb: totalPayable,
        deliveryFromDb: storedDelivery,
        calculatedBase: window.currentBasePrice
    });

    // --- 3. POPULATE INPUTS ---
    document.getElementById('adv_orderRef').value = orderRef;
    document.getElementById('adv_customerId').value = custId;

    // ✅ NEW: Populate Visual Fields
    const productInput = document.getElementById('adv_productPrice');
    const discountInput = document.getElementById('adv_discount');

    if (productInput) productInput.value = productPrice.toLocaleString('en-BD', { minimumFractionDigits: 2 });
    if (discountInput) discountInput.value = discount.toLocaleString('en-BD', { minimumFractionDigits: 2 });

    // Set Editable Delivery Field
    document.getElementById('adv_delivery').value = storedDelivery;

    // Set Read-Only Total Field (Initially matches DB)
    document.getElementById('adv_netAmount').value = totalPayable;

    // Set History
    document.getElementById('adv_alreadyPaid').value = alreadyPaid;
    document.getElementById('adv_displayPaid').textContent = alreadyPaid.toLocaleString('en-BD', { minimumFractionDigits: 2 });

    // Reset User Fields
    document.getElementById('adv_paidAmount').value = "";
    document.getElementById('adv_note').value = "";

    // Reset Dropdown
    const typeSelect = document.getElementById('adv_paymentType');
    if (typeSelect) typeSelect.value = "Advance";

    // Clear Validation Styles
    const paidInput = document.getElementById('adv_paidAmount');
    const submitBtn = document.getElementById('adv_submitBtn');
    if (paidInput) paidInput.classList.remove('is-invalid');
    if (submitBtn) submitBtn.disabled = false;

    // --- 4. TRIGGER INITIAL CALCULATION ---
    // This ensures "Current Due" is correct based on the inputs immediately
    if (typeof window.calculateTotals === 'function') {
        window.calculateTotals();
    }

    // --- 5. SHOW MODAL ---
    const modalEl = document.getElementById('advanceModal');
    // Ensure modal is in body (prevents z-index/overlay issues)
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

    // --- A. GLOBAL CALCULATION LOGIC ---
    // Defined globally so openAdvanceModal can call it
    window.calculateTotals = function () {
        // Re-select elements to ensure valid references
        const netInput = document.getElementById('adv_netAmount');
        const deliveryInput = document.getElementById('adv_delivery');
        const alreadyPaidInput = document.getElementById('adv_alreadyPaid');
        const payInput = document.getElementById('adv_paidAmount');
        const submitBtn = document.getElementById('adv_submitBtn');

        const displayCurrentDue = document.getElementById('adv_displayCurrentDue');
        const displayBalance = document.getElementById('adv_dueAmount');

        // 1. Get Live Values
        const newDelivery = parseFloat(deliveryInput.value) || 0;
        const alreadyPaid = parseFloat(alreadyPaidInput.value) || 0;
        const payingNow = parseFloat(payInput.value) || 0;

        // 2. Use Global Base Price (Product Cost)
        // If undefined, fallback to 0
        const base = window.currentBasePrice || 0;

        // 3. Calculate New Total (Base + Editable Delivery)
        const newTotalPayable = base + newDelivery;

        // 4. Update the Read-Only Total Field
        if (netInput) netInput.value = newTotalPayable; // Update UI

        // 5. Calculate Current Due
        const currentDue = newTotalPayable - alreadyPaid;

        // 6. Update "Current Due" Display
        if (displayCurrentDue) {
            if (currentDue <= 0) {
                displayCurrentDue.innerHTML = '<span class="text-success"><i class="fas fa-check"></i> Fully Paid</span>';
            } else {
                displayCurrentDue.textContent = currentDue.toFixed(2);
            }
        }

        // 7. Calculate Balance After Payment
        const balanceAfter = currentDue - payingNow;

        // 8. Update Balance Display
        if (displayBalance) {
            displayBalance.textContent = balanceAfter.toFixed(2);
            if (balanceAfter < 0) {
                // Overpayment
                displayBalance.parentElement.className = "text-success fw-bold fs-5";
            } else {
                // Remaining Due
                displayBalance.parentElement.className = "text-danger fw-bold fs-5";
            }
        }

        // 9. Validation (Prevent Paying > Due)
        if (payingNow > (currentDue + 0.5) && currentDue > 0) {
            if (payInput) payInput.classList.add('is-invalid');
            if (submitBtn) submitBtn.disabled = true;
        } else {
            if (payInput) payInput.classList.remove('is-invalid');
            if (submitBtn) submitBtn.disabled = false;
        }

        return { currentDue, payingNow };
    };

    // --- B. LISTENERS ---

    // 1. Delivery Change -> Updates Total & Due
    const deliveryInput = document.getElementById('adv_delivery');
    if (deliveryInput) {
        deliveryInput.addEventListener('input', window.calculateTotals);
    }

    // 2. Amount Change -> Updates Balance & Validation
    const payInput = document.getElementById('adv_paidAmount');
    const typeSelect = document.getElementById('adv_paymentType');

    if (payInput) {
        payInput.addEventListener('input', function () {
            const { currentDue, payingNow } = window.calculateTotals();

            // Auto-switch dropdown
            if (typeSelect && payingNow <= currentDue) {
                if (payingNow >= currentDue && currentDue > 0) typeSelect.value = "Sale";
                else typeSelect.value = "Advance";
            }
        });
    }

    // 3. Dropdown Change
    if (typeSelect) {
        typeSelect.addEventListener('change', function () {
            const { currentDue } = window.calculateTotals();
            if (this.value === 'Sale') {
                payInput.value = currentDue > 0 ? currentDue : 0;
            } else {
                payInput.value = "";
            }
            window.calculateTotals();
        });
    }

    // --- C. SUBMIT FORM ---
    const advanceForm = document.getElementById('advanceForm');
    if (advanceForm) {
        advanceForm.addEventListener('submit', function (e) {
            e.preventDefault();

            // Grab the FINAL delivery charge from input
            const deliveryVal = parseFloat(document.getElementById('adv_delivery').value) || 0;

            const payload = {
                CustomerId: parseInt(document.getElementById('adv_customerId').value),
                PaymentMethodId: parseInt(document.getElementById('adv_paymentMethod').value),
                PaymentType: document.getElementById('adv_paymentType').value,
                Amount: parseFloat(document.getElementById('adv_paidAmount').value),
                TransactionReference: document.getElementById('adv_orderRef').value,
                Notes: document.getElementById('adv_note').value,
                // Send new delivery to update Order Header
                DeliveryCharge: deliveryVal
            };

            fetch('/order/add-payment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            }).then(r => r.json()).then(data => {
                if (data.success) {
                    alert("Payment Added & Order Updated Successfully!");
                    location.reload();
                } else {
                    alert("Error: " + data.message);
                }
            }).catch(err => alert("Network Error"));
        });
    }

    // --- D. TOGGLE CONFIRMATION (Existing Logic) ---
    const toggles = document.querySelectorAll('.confirm-toggle');
    toggles.forEach(toggle => {
        toggle.addEventListener('change', function () {
            const checkbox = this;
            const orderId = checkbox.getAttribute('data-id');
            const isConfirmed = checkbox.checked;
            const statusBadge = document.getElementById(`status-badge-${orderId}`);

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
});