/**
 * Enhanced Approval Workflow JavaScript Helper
 * Handles group-based parallel approvals with sequential steps
 */

class EnhancedApprovalWorkflow {
    constructor(containerId, requestId) {
        this.container = document.getElementById(containerId);
        this.requestId = requestId;
        this.workflow = null;
        this.selectedTemplate = 'amount-based';
        
        this.init();
    }

    async init() {
        await this.loadWorkflow();
        this.renderWorkflow();
        this.bindEvents();
    }

    async loadWorkflow(templateType = 'amount-based') {
        try {
            const response = await fetch(`/TradingLimitRequest/GetWorkflowTemplate?templateType=${templateType}&requestId=${this.requestId}`);
            this.workflow = await response.json();
            this.selectedTemplate = templateType;
        } catch (error) {
            console.error('Error loading workflow:', error);
            this.workflow = { Steps: [], Status: 'Error' };
        }
    }

    renderWorkflow() {
        if (!this.container || !this.workflow) return;

        const html = `
            <div class="enhanced-workflow-container">
                <!-- Template Selector -->
                <div class="workflow-template-selector mb-4">
                    <h5><i class="fas fa-cogs me-2"></i>Approval Workflow Template</h5>
                    <div class="btn-group" role="group">
                        <button type="button" class="btn ${this.selectedTemplate === 'simple' ? 'btn-primary' : 'btn-outline-primary'}" 
                                onclick="workflowManager.changeTemplate('simple')">
                            <i class="fas fa-bolt me-1"></i>Simple
                        </button>
                        <button type="button" class="btn ${this.selectedTemplate === 'standard' ? 'btn-primary' : 'btn-outline-primary'}" 
                                onclick="workflowManager.changeTemplate('standard')">
                            <i class="fas fa-sitemap me-1"></i>Standard
                        </button>
                        <button type="button" class="btn ${this.selectedTemplate === 'complex' ? 'btn-primary' : 'btn-outline-primary'}" 
                                onclick="workflowManager.changeTemplate('complex')">
                            <i class="fas fa-project-diagram me-1"></i>Complex
                        </button>
                        <button type="button" class="btn ${this.selectedTemplate === 'parallel' ? 'btn-primary' : 'btn-outline-primary'}" 
                                onclick="workflowManager.changeTemplate('parallel')">
                            <i class="fas fa-code-branch me-1"></i>Parallel
                        </button>
                        <button type="button" class="btn ${this.selectedTemplate === 'amount-based' ? 'btn-primary' : 'btn-outline-primary'}" 
                                onclick="workflowManager.changeTemplate('amount-based')">
                            <i class="fas fa-calculator me-1"></i>Amount-Based
                        </button>
                    </div>
                </div>

                <!-- Workflow Progress -->
                <div class="workflow-progress mb-4">
                    <div class="d-flex justify-content-between align-items-center mb-2">
                        <h6><i class="fas fa-tasks me-2"></i>Workflow Progress</h6>
                        <span class="badge bg-info">${this.workflow.OverallProgress?.toFixed(1) || 0}% Complete</span>
                    </div>
                    <div class="progress">
                        <div class="progress-bar" role="progressbar" 
                             style="width: ${this.workflow.OverallProgress || 0}%">
                        </div>
                    </div>
                </div>

                <!-- Approval Steps -->
                <div class="approval-steps">
                    ${this.renderSteps()}
                </div>

                <!-- Action Buttons -->
                <div class="workflow-actions mt-4">
                    <button type="button" class="btn btn-success" onclick="workflowManager.submitWorkflow()">
                        <i class="fas fa-paper-plane me-2"></i>Submit for Approval
                    </button>
                    <button type="button" class="btn btn-secondary" onclick="workflowManager.saveAsDraft()">
                        <i class="fas fa-save me-2"></i>Save as Draft
                    </button>
                    <button type="button" class="btn btn-info" onclick="workflowManager.previewWorkflow()">
                        <i class="fas fa-eye me-2"></i>Preview Workflow
                    </button>
                </div>
            </div>
        `;

        this.container.innerHTML = html;
    }

    renderSteps() {
        if (!this.workflow.Steps || this.workflow.Steps.length === 0) {
            return '<div class="alert alert-warning">No approval steps configured</div>';
        }

        return this.workflow.Steps.map((step, index) => this.renderStep(step, index)).join('');
    }

    renderStep(step, index) {
        const isActive = step.IsActive;
        const isComplete = step.IsComplete;
        const statusIcon = isComplete ? 'fas fa-check-circle text-success' : 
                          isActive ? 'fas fa-clock text-warning' : 
                          'fas fa-circle text-muted';

        const approversList = step.Approvers.map(approver => `
            <div class="approver-item d-flex align-items-center justify-content-between">
                <div>
                    <strong>${approver.Name}</strong>
                    <br>
                    <small class="text-muted">${approver.Email}</small>
                </div>
                <span class="badge bg-secondary">${approver.Role}</span>
            </div>
        `).join('');

        return `
            <div class="approval-step-card card mb-3 ${isActive ? 'border-warning' : isComplete ? 'border-success' : ''}">
                <div class="card-header d-flex justify-content-between align-items-center">
                    <div>
                        <i class="${statusIcon} me-2"></i>
                        <strong>Step ${step.StepNumber}: ${step.GroupName}</strong>
                        <span class="badge bg-primary ms-2">${step.ApprovalType.replace('Parallel', '').replace('Any', ' Any ')}</span>
                    </div>
                    <div class="step-progress">
                        <small>${step.ApprovalsReceived || 0} of ${step.RequiredApprovals} approved</small>
                        <div class="progress progress-sm mt-1" style="width: 100px; height: 4px;">
                            <div class="progress-bar" style="width: ${step.ProgressPercentage || 0}%"></div>
                        </div>
                    </div>
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-8">
                            <h6>Approvers (${step.Approvers.length})</h6>
                            <div class="approvers-list">
                                ${approversList}
                            </div>
                        </div>
                        <div class="col-md-4">
                            <div class="step-details">
                                <p><strong>Due Date:</strong> ${new Date(step.DueDate).toLocaleDateString()}</p>
                                <p><strong>Required:</strong> ${step.IsRequired ? 'Yes' : 'No'}</p>
                                <p><strong>Status:</strong> 
                                    <span class="badge ${this.getStatusBadgeClass(step.Status)}">${step.Status}</span>
                                </p>
                                ${step.Conditions ? `<p><strong>Conditions:</strong> ${step.Conditions}</p>` : ''}
                            </div>
                        </div>
                    </div>
                    
                    <!-- Approval Description -->
                    <div class="mt-3">
                        <div class="alert alert-info mb-0">
                            <i class="fas fa-info-circle me-2"></i>
                            ${step.ApprovalDescription || 'Standard approval process'}
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    getStatusBadgeClass(status) {
        switch (status) {
            case 'Approved': return 'bg-success';
            case 'Rejected': return 'bg-danger';
            case 'InProgress': return 'bg-warning';
            case 'Pending': return 'bg-secondary';
            default: return 'bg-light text-dark';
        }
    }

    async changeTemplate(templateType) {
        this.showLoading(true);
        await this.loadWorkflow(templateType);
        this.renderWorkflow();
        this.showLoading(false);
    }

    showLoading(show) {
        if (show) {
            this.container.innerHTML = `
                <div class="d-flex justify-content-center align-items-center" style="height: 200px;">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <span class="ms-3">Loading workflow...</span>
                </div>
            `;
        }
    }

    async submitWorkflow() {
        try {
            // Convert workflow to submission format
            const submissionData = {
                Id: this.requestId,
                WorkflowType: this.selectedTemplate,
                ApprovalSteps: this.workflow.Steps.flatMap(step => 
                    step.Approvers.map((approver, index) => ({
                        StepNumber: step.StepNumber,
                        Email: approver.Email,
                        Name: approver.Name,
                        Role: approver.Role,
                        IsRequired: step.IsRequired,
                        DueDate: step.DueDate,
                        ApprovalConditions: step.Conditions || '',
                        GroupId: step.GroupId
                    }))
                )
            };

            // Submit via form
            const form = document.createElement('form');
            form.method = 'POST';
            form.action = '/TradingLimitRequest/SubmitMultiApproval';
            
            // Add CSRF token
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) {
                form.appendChild(token.cloneNode(true));
            }

            // Add data as hidden inputs
            this.addHiddenInput(form, 'Id', submissionData.Id);
            this.addHiddenInput(form, 'WorkflowType', submissionData.WorkflowType);
            
            submissionData.ApprovalSteps.forEach((step, index) => {
                this.addHiddenInput(form, `ApprovalSteps[${index}].StepNumber`, step.StepNumber);
                this.addHiddenInput(form, `ApprovalSteps[${index}].Email`, step.Email);
                this.addHiddenInput(form, `ApprovalSteps[${index}].Name`, step.Name);
                this.addHiddenInput(form, `ApprovalSteps[${index}].Role`, step.Role);
                this.addHiddenInput(form, `ApprovalSteps[${index}].IsRequired`, step.IsRequired);
                this.addHiddenInput(form, `ApprovalSteps[${index}].DueDate`, step.DueDate);
                this.addHiddenInput(form, `ApprovalSteps[${index}].ApprovalConditions`, step.ApprovalConditions);
            });

            document.body.appendChild(form);
            form.submit();

        } catch (error) {
            console.error('Error submitting workflow:', error);
            alert('Error submitting workflow. Please try again.');
        }
    }

    addHiddenInput(form, name, value) {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = name;
        input.value = value || '';
        form.appendChild(input);
    }

    saveAsDraft() {
        alert('Draft functionality will be implemented based on your requirements.');
    }

    previewWorkflow() {
        const modal = new bootstrap.Modal(document.getElementById('workflowPreviewModal') || this.createPreviewModal());
        modal.show();
    }

    createPreviewModal() {
        const modal = document.createElement('div');
        modal.className = 'modal fade';
        modal.id = 'workflowPreviewModal';
        modal.innerHTML = `
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Workflow Preview</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <pre>${JSON.stringify(this.workflow, null, 2)}</pre>
                    </div>
                </div>
            </div>
        `;
        document.body.appendChild(modal);
        return modal;
    }

    bindEvents() {
        // Add any additional event bindings here
    }
}

// Global workflow manager instance
let workflowManager = null;

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    const workflowContainer = document.getElementById('enhanced-workflow-container');
    if (workflowContainer) {
        const requestId = workflowContainer.dataset.requestId;
        if (requestId) {
            workflowManager = new EnhancedApprovalWorkflow('enhanced-workflow-container', requestId);
        }
    }
});