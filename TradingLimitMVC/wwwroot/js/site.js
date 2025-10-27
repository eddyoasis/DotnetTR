// Global site functionality
$(document).ready(function () {


    // Initialize tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Auto-hide alerts after 5 seconds
    setTimeout(function () {
        $('.alert').fadeOut('slow');
    }, 5000);

    // Add loading state to buttons on form submit
    $('form').on('submit', function () {
        var submitBtn = $(this).find('button[type="submit"]');
        var originalText = submitBtn.html();
        submitBtn.html('<i class="fas fa-spinner fa-spin me-2"></i>Processing...');
        submitBtn.prop('disabled', true);

        // Re-enable button after 5 seconds (in case of error)
        setTimeout(function () {
            submitBtn.html(originalText);
            submitBtn.prop('disabled', false);
        }, 5000);
    });

    // Confirm delete actions
    $('a[href*="Delete"], button[data-action="delete"]').on('click', function (e) {
        if (!confirm('Are you sure you want to delete this item? This action cannot be undone.')) {
            e.preventDefault();
            return false;
        }
    });

    // Format currency inputs
    $('input[type="number"][step="0.01"]').on('blur', function () {
        var value = parseFloat($(this).val());
        if (!isNaN(value)) {
            $(this).val(value.toFixed(2));
        }
    });

    // Auto-calculate totals in tables
    $('.table').on('input', 'input[type="number"]', function () {
        calculateRowTotal($(this).closest('tr'));
        calculateTableTotal($(this).closest('table'));
    });

    // Enhanced file upload with drag and drop
    initializeFileUpload();

    // Form validation enhancements
    enhanceFormValidation();

    // Initialize date pickers with min date as today
    $('input[type="date"]').each(function () {
        if ($(this).attr('name').toLowerCase().includes('delivery') ||
            $(this).attr('name').toLowerCase().includes('expected')) {
            var today = new Date().toISOString().split('T')[0];
            $(this).attr('min', today);
        }
    });

    // Auto-save form data to localStorage (except sensitive fields)
    enableAutoSave();


});

// File upload functionality with drag and drop
function initializeFileUpload() {
    $(’.file - upload - area’).each(function () {
        var $uploadArea = $(this);
        var $fileInput = $uploadArea.find(‘input[type = "file"]’);


        // Drag and drop events
        $uploadArea.on('dragover dragenter', function (e) {
            e.preventDefault();
            e.stopPropagation();
            $(this).addClass('drag-over');
        });

        $uploadArea.on('dragleave dragend drop', function (e) {
            e.preventDefault();
            e.stopPropagation();
            $(this).removeClass('drag-over');
        });

        $uploadArea.on('drop', function (e) {
            var files = e.originalEvent.dataTransfer.files;
            handleFiles(files, $fileInput);
        });

        // Click to upload
        $uploadArea.on('click', function () {
            $fileInput.click();
        });

        // File input change
        $fileInput.on('change', function () {
            handleFiles(this.files, $(this));
        });
    });


}

// Handle file selection and validation
function handleFiles(files, $input) {
    var allowedTypes = [‘application / pdf’, ‘application / msword’,
‘application / vnd.openxmlformats - officedocument.wordprocessingml.document’,
‘application / vnd.ms - excel’,
‘application / vnd.openxmlformats - officedocument.spreadsheetml.sheet’,
‘image / jpeg’, ‘image / png’];
    var maxSize = 10 * 1024 * 1024; // 10MB


    for (var i = 0; i < files.length; i++) {
        var file = files[i];

        // Validate file type
        if (!allowedTypes.includes(file.type)) {
            showAlert('Invalid file type: ' + file.name, 'danger');
            continue;
        }

        // Validate file size
        if (file.size > maxSize) {
            showAlert('File too large: ' + file.name + ' (max 10MB)', 'danger');
            continue;
        }

        // Add file to preview list
        addFilePreview(file, $input.closest('.form-section'));
    }


}

// Add file preview to the upload area
function addFilePreview(file, $container) {
    var fileSize = (file.size / 1024).toFixed(2) + ’ KB’;
    if (file.size > 1024 * 1024) {
        fileSize = (file.size / (1024 * 1024)).toFixed(2) + ’ MB’;
    }


    var fileIcon = getFileIcon(file.type);

    var $preview = $('<div class="file-preview-item d-flex justify-content-between align-items-center p-2 border rounded mb-2">' +
        '<div>' +
        '<i class="' + fileIcon + ' me-2"></i>' +
        '<span class="file-name">' + file.name + '</span>' +
        '<small class="text-muted d-block">' + fileSize + '</small>' +
        '</div>' +
        '<button type="button" class="btn btn-sm btn-outline-danger remove-file">' +
        '<i class="fas fa-times"></i>' +
        '</button>' +
        '</div>');

    $preview.find('.remove-file').on('click', function () {
        $preview.remove();
    });

    $container.find('.file-preview-list').append($preview);


}

// Get appropriate icon for file type
function getFileIcon(fileType) {
    if (fileType.includes(‘pdf’)) return ‘fas fa - file - pdf text - danger’;
    if (fileType.includes(‘word’)) return ‘fas fa - file - word text - primary’;
    if (fileType.includes(‘excel’) || fileType.includes(‘sheet’)) return ‘fas fa - file - excel text - success’;
    if (fileType.includes(‘image’)) return ‘fas fa - file - image text - info’;
    return ‘fas fa - file text - muted’;
}

// Calculate row total in purchase tables
function calculateRowTotal($row) {
    var quantity = parseFloat($row.find(‘input[data - field= "quantity"]’).val()) || 0;
    var unitPrice = parseFloat($row.find(‘input[data - field= "unitPrice"]’).val()) || 0;
    var discountPercent = parseFloat($row.find(‘input[data - field= "discountPercent"]’).val()) || 0;
    var discountAmount = parseFloat($row.find(‘input[data - field= "discountAmount"]’).val()) || 0;


    var subtotal = quantity * unitPrice;
    var discountFromPercent = subtotal * (discountPercent / 100);
    var total = subtotal - discountFromPercent - discountAmount;

    $row.find('input[data-field="amount"]').val(total.toFixed(2));


}

// Calculate table total
function calculateTableTotal($table) {
    var total = 0;
    $table.find(‘tbody tr’).each(function () {
        var rowTotal = parseFloat($(this).find(‘input[data - field= "amount"]’).val()) || 0;
        total += rowTotal;
    });


    $table.find('.table-total').text('$' + total.toFixed(2));


}

// Enhanced form validation
function enhanceFormValidation() {
    // Real-time validation for required fields
    $(‘input[required], select[required], textarea[required]’).on(‘blur’, function () {
        validateField($(this));
    });


    // Email validation
    $('input[type="email"]').on('blur', function () {
        validateEmail($(this));
    });

    // Phone number formatting
    $('input[data-field="phone"]').on('input', function () {
        formatPhoneNumber($(this));
    });

    // Prevent form submission if validation fails
    $('form').on('submit', function (e) {
        var isValid = true;
        $(this).find('input[required], select[required], textarea[required]').each(function () {
            if (!validateField($(this))) {
                isValid = false;
            }
        });

        if (!isValid) {
            e.preventDefault();
            showAlert('Please correct the errors in the form before submitting.', 'danger');
            return false;
        }
    });


}

// Validate individual field
function validateField($field) {
    var value = $field.val().trim();
    var isValid = true;
    var errorMessage = ‘’;


    // Required field validation
    if ($field.prop('required') && !value) {
        isValid = false;
        errorMessage = 'This field is required.';
    }

    // Specific validations based on field type
    if (value && $field.attr('type') === 'email') {
        var emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(value)) {
            isValid = false;
            errorMessage = 'Please enter a valid email address.';
        }
    }

    // Update field appearance
    if (isValid) {
        $field.removeClass('is-invalid').addClass('is-valid');
        $field.siblings('.invalid-feedback').hide();
    } else {
        $field.removeClass('is-valid').addClass('is-invalid');
        var $feedback = $field.siblings('.invalid-feedback');
        if ($feedback.length === 0) {
            $feedback = $('<div class="invalid-feedback"></div>');
            $field.after($feedback);
        }
        $feedback.text(errorMessage).show();
    }

    return isValid;


}

// Email validation
function validateEmail($field) {
    var email = $field.val().trim();
    if (email) {
        var emailRegex = /^[^\s@]+@[^\s@]+.[^\s@]+$/;
        if (!emailRegex.test(email)) {
            $field.addClass(‘is - invalid’);
            return false;
        } else {
            $field.removeClass(‘is - invalid’).addClass(‘is - valid’);
            return true;
        }
    }
    return true;
}

// Format phone number
function formatPhoneNumber($field) {
    var phone = $field.val().replace(/\D/g, ‘’);
    if (phone.length >= 10) {
        phone = phone.substring(0, 10);
        var formatted = ‘(’ + phone.substring(0, 3) + ’) ’ +
            phone.substring(3, 6) + ‘-’ +
                phone.substring(6, 10);
        $field.val(formatted);
    }
}

// Auto-save functionality
function enableAutoSave() {
    var sensitiveFields = [‘password’, ‘ssn’, ‘credit’, ‘card’, ‘cvv’];


    $('form input, form select, form textarea').on('change input', function () {
        var fieldName = $(this).attr('name') || $(this).attr('id');
        var fieldValue = $(this).val();

        // Don't save sensitive data
        var isSensitive = sensitiveFields.some(function (sensitive) {
            return fieldName && fieldName.toLowerCase().includes(sensitive);
        });

        if (!isSensitive && fieldName && fieldValue) {
            localStorage.setItem('autosave_' + fieldName, fieldValue);
        }
    });

    // Restore saved data on page load
    $('form input, form select, form textarea').each(function () {
        var fieldName = $(this).attr('name') || $(this).attr('id');
        if (fieldName) {
            var savedValue = localStorage.getItem('autosave_' + fieldName);
            if (savedValue && !$(this).val()) {
                $(this).val(savedValue);
            }
        }
    });

    // Clear auto-save data on successful form submission
    $('form').on('submit', function () {
        var $form = $(this);
        setTimeout(function () {
            $form.find('input, select, textarea').each(function () {
                var fieldName = $(this).attr('name') || $(this).attr('id');
                if (fieldName) {
                    localStorage.removeItem('autosave_' + fieldName);
                }
            });
        }, 1000);
    });


}

// Show alert messages
function showAlert(message, type) {
    type = type || ‘info’;
    var alertClass = ‘alert-’ + type;
    var iconClass = type === ‘danger’ ? ‘fa-exclamation - circle’ :
    type === ‘success’ ? ‘fa - check - circle’ :
    type === ‘warning’ ? ‘fa - exclamation - triangle’ : ‘fa - info - circle’;


    var $alert = $('<div class="alert ' + alertClass + ' alert-dismissible fade show" role="alert">' +
        '<i class="fas ' + iconClass + ' me-2"></i>' + message +
        '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>' +
        '</div>');

    // Insert alert at the top of the main content
    $('.main-content').prepend($alert);

    // Auto-hide after 5 seconds
    setTimeout(function () {
        $alert.fadeOut();
    }, 5000);


}

// Purchase Order specific functions
function addPurchaseOrderRow() {
    var $table = $(’#purchaseItemsTable tbody’);
    var rowCount = $table.find(‘tr’).length;


    var newRow = '<tr>' +
        '<td><input type="text" class="form-control form-control-sm" data-field="description" placeholder="Description"></td>' +
        '<td><input type="text" class="form-control form-control-sm" data-field="details" placeholder="Details"></td>' +
        '<td><input type="number" class="form-control form-control-sm" data-field="quantity" value="1" min="1"></td>' +
        '<td><input type="number" class="form-control form-control-sm" data-field="unitPrice" step="0.01" placeholder="0.00"></td>' +
        '<td><input type="number" class="form-control form-control-sm" data-field="discountPercent" step="0.01" placeholder="0"></td>' +
        '<td><input type="number" class="form-control form-control-sm" data-field="discountAmount" step="0.01" placeholder="0"></td>' +
        '<td>' +
        '<select class="form-select form-select-sm" data-field="gst">' +
        '<option value="">GST</option>' +
        '<option value="0">0%</option>' +
        '<option value="7">7%</option>' +
        '</select>' +
        '</td>' +
        '<td><input type="number" class="form-control form-control-sm" data-field="amount" readonly placeholder="0.00"></td>' +
        '<td><input type="text" class="form-control form-control-sm" data-field="prNo" placeholder="PR No"></td>' +
        '<td>' +
        '<button type="button" class="btn btn-sm btn-danger" onclick="removePurchaseOrderRow(this)">' +
        '<i class="fas fa-trash"></i>' +
        '</button>' +
        '</td>' +
        '</tr>';

    $table.append(newRow);


}

function removePurchaseOrderRow(button) {
    var $row = $(button).closest(‘tr’);
    var $table = $row.closest(‘table’);


    if ($table.find('tbody tr').length > 1) {
        $row.remove();
        calculateTableTotal($table);
    } else {
        showAlert('At least one row is required.', 'warning');
    }


}

// Search and filter functionality
function initializeSearch() {
    $(’#searchInput’).on(‘keyup’, function () {
        var value = $(this).val().toLowerCase();
        $(‘table tbody tr’).filter(function () {
            $(this).toggle($(this).text().toLowerCase().indexOf(value) > -1);
        });
    });
}

// Print functionality
function printDocument() {
    window.print();
}

// Export to PDF (placeholder - would need server-side implementation)
function exportToPDF() {
    showAlert(‘PDF export functionality will be implemented with server - side support.’, ‘info’);
}

// Dark mode toggle (optional enhancement)
function toggleDarkMode() {
    $(‘body’).toggleClass(‘dark - mode’);
    var isDark = $(‘body’).hasClass(‘dark - mode’);
    localStorage.setItem(‘darkMode’, isDark);
}

// Initialize dark mode from localStorage
$(document).ready(function () {
    if (localStorage.getItem(‘darkMode’) === ‘true’) {
    $(‘body’).addClass(‘dark - mode’);
}
});

// Utility functions
function formatCurrency(amount) {
    return new Intl.NumberFormat(‘en - US’, {
        style: ‘currency’,
        currency: ‘USD’
    }).format(amount);
}

function formatDate(date) {
    return new Date(date).toLocaleDateString(‘en - US’, {
        year: ‘numeric’,
        month: ‘short’,
        day: ‘numeric’
    });
}



// Auto-calculate amounts in forms
function calculateItemAmount(row) {
    const quantity = parseFloat(row.find('input[name*="Quantity"]').val()) || 0;
    const unitPrice = parseFloat(row.find('input[name*="UnitPrice"]').val()) || 0;
    const discountPercent = parseFloat(row.find('input[name*="DiscountPercent"]').val()) || 0;
    const discountAmount = parseFloat(row.find('input[name*="DiscountAmount"]').val()) || 0;
    // Calculate subtotal BEFORE discount
    let subtotal = quantity * unitPrice;
    // Apply discount (either percentage OR fixed amount, not both)
    let discount = 0;
    if (discountPercent > 0) {
        discount = (subtotal * discountPercent) / 100;
        // Auto-fill discount amount field for display
        row.find('input[name*="DiscountAmount"]').val(discount.toFixed(2));
    } else if (discountAmount > 0) {
        discount = discountAmount;
    }
    // Final amount = subtotal - discount (THIS IS THE DISCOUNTED PRICE)
    const finalAmount = subtotal - discount;
    row.find('input[name*="Amount"]').val(finalAmount.toFixed(2));
    calculateTotalAmount();
}
function calculateTotalAmount() {
    let total = 0;
    $('input[name*="Amount"][readonly]').each(function () {
        total += parseFloat($(this).val()) || 0;
    });
    // Update display
    if ($('#totalAmount').length) {
        $('#totalAmount').text('$' + total.toFixed(2));
    }
    // : Also update the quotation total field
    if ($('#totalQuotationAmount').length) {
        $('#totalQuotationAmount').val(total.toFixed(2));
    }
}

// Auto-dismiss alerts after 5 seconds
$(document).ready(function () {
    setTimeout(function () {
        $(’.alert’).fadeOut(‘slow’);
    }, 5000);


    // Initialize calculation for existing items
    $('input[name*="Quantity"], input[name*="UnitPrice"], input[name*="DiscountPercent"], input[name*="DiscountAmount"]').on('change', function () {
        calculateItemAmount($(this).closest('tr'));
    });

    // Form validation
    $('form').on('submit', function (e) {
        let isValid = true;
        $(this).find('input[required], select[required], textarea[required]').each(function () {
            if (!$(this).val().trim()) {
                $(this).addClass('is-invalid');
                isValid = false;
            } else {
                $(this).removeClass('is-invalid');
            }
        });

        if (!isValid) {
            e.preventDefault();
            alert('Please fill in all required fields.');
        }
    });

    // Dynamic row addition for purchase items
    $('#addItemBtn').on('click', function () {
        // This would be implemented based on your specific table structure
        addNewItemRow();
    });


});

// Show loading spinner during form submissions
function showLoading(button) {
    button.html(’<span class="spinner-border spinner-border-sm me-2"></span>Processing…’);
    button.prop(‘disabled’, true);
}

// Confirmation dialogs for important actions
function confirmAction(message) {
    return confirm(message);
}

// Format currency inputs
function formatCurrency(input) {
    let value = parseFloat(input.value) || 0;
    input.value = value.toFixed(2);
}

// File upload validation
function validateFile(input) {
    const allowedTypes = [’.pdf’, ‘.doc’, ‘.docx’, ‘.xls’, ‘.xlsx’, ‘.jpg’, ‘.png’];
    const maxSize = 5 * 1024 * 1024; // 5MB


    for (let file of input.files) {
        const fileExtension = '.' + file.name.split('.').pop().toLowerCase();

        if (!allowedTypes.includes(fileExtension)) {
            alert('Invalid file type. Please select: ' + allowedTypes.join(', '));
            input.value = '';
            return false;
        }

        if (file.size > maxSize) {
            alert('File size too large. Maximum size is 5MB.');
            input.value = '';
            return false;
        }
    }
    return true;


}
