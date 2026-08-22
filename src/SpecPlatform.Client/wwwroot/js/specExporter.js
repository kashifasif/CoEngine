window.exportSpecToPdf = function (spec) {
    var printWindow = window.open('', '_blank', 'width=950,height=1100');
    if (!printWindow) {
        alert("Please allow popups to export the PDF specification.");
        return;
    }

    var criteriaHtml = spec.criteria && spec.criteria.length > 0
        ? spec.criteria.map(function(c, i) {
            var safeC = String(c).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            return '<div style="background:#f8fafc; border:1px solid #e2e8f0; border-left:4px solid #4f46e5; padding:12px 16px; margin-bottom:8px; border-radius:6px; font-size:13px; line-height:1.5;">' +
                   '<span style="background:#4f46e5; color:#ffffff; font-family:monospace; font-weight:700; padding:2px 8px; border-radius:4px; font-size:11px; margin-right:10px;">AC-' + (i+1) + '</span>' +
                   '<span>' + safeC + '</span>' +
                   '</div>';
        }).join('')
        : '<p style="color:#64748b; font-style:italic;">No acceptance criteria defined.</p>';

    var tagsHtml = spec.tags && spec.tags.length > 0
        ? spec.tags.map(function(t) {
            var safeT = String(t).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            return '<span style="background:#e0e7ff; color:#3730a3; font-family:monospace; font-size:11px; padding:3px 10px; border-radius:15px; font-weight:600; margin-right:6px; display:inline-block;">🏷️ ' + safeT + '</span>';
        }).join('')
        : '<span style="color:#64748b; font-size:12px;">General Scope</span>';

    var safeTitle = String(spec.title || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    var safeProject = String(spec.projectName || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    var safeDesc = String(spec.description || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

    var htmlContent = '<!DOCTYPE html><html><head>' +
        '<title>' + safeProject + ' - ' + safeTitle + ' (v' + spec.version + ') Specification</title>' +
        '<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css">' +
        '<style>' +
        '@media print { @page { margin: 15mm; size: A4 portrait; } body { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; } .no-print { display: none !important; } }' +
        'body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; color: #0f172a; background: #ffffff; padding: 32px; }' +
        '.header-box { border-bottom: 3px solid #4f46e5; padding-bottom: 18px; margin-bottom: 24px; }' +
        '.badge-spec { background: #e0e7ff; color: #4338ca; font-weight: 700; font-family: monospace; padding: 4px 10px; border-radius: 6px; font-size: 12px; }' +
        '.badge-ver { background: #dcfce7; color: #15803d; font-weight: 700; font-family: monospace; padding: 4px 10px; border-radius: 6px; font-size: 12px; }' +
        '.section-title { font-family: monospace; font-size: 12px; font-weight: 800; color: #4f46e5; letter-spacing: 0.08em; text-transform: uppercase; margin-bottom: 10px; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; margin-top: 24px; }' +
        '.nfr-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px 14px; height: 100%; font-size: 12px; }' +
        '</style></head><body>' +
        '<div class="no-print d-flex justify-content-between align-items-center bg-dark text-white p-3 rounded mb-4 shadow-sm">' +
        '<div><strong>📥 Business Specification PDF Ready:</strong> Choose <code>Save as PDF</code> in print dialog.</div>' +
        '<button class="btn btn-success btn-sm font-mono px-4 fw-bold" onclick="window.print();">🖨️ Download PDF / Print</button>' +
        '</div>' +
        '<div class="header-box d-flex justify-content-between align-items-start">' +
        '<div>' +
        '<div class="d-flex align-items-center gap-2 mb-2">' +
        '<span class="badge-spec">SPEC-00' + spec.id + '</span>' +
        '<span class="badge-ver">RELEASE v' + spec.version + '</span>' +
        '<span class="badge bg-secondary font-mono">' + (spec.status || 'Published') + '</span>' +
        '</div>' +
        '<h1 class="fw-bold fs-3 text-dark mb-1">' + safeTitle + '</h1>' +
        '<div class="text-secondary font-mono small">PROJECT: <strong>' + safeProject + '</strong> | DATE: ' + spec.exportDate + '</div>' +
        '</div>' +
        '<div class="text-end font-mono small text-muted">' +
        '<div>Document Type: Business Master Spec</div>' +
        '<div>Author: Product Owner / BA Team</div>' +
        '</div>' +
        '</div>' +
        '<div>' +
        '<div class="section-title">1. EXECUTIVE OVERVIEW & BUSINESS INTENT</div>' +
        '<div class="p-3 bg-light rounded border text-dark lh-base" style="white-space: pre-wrap; font-size: 13px;">' + safeDesc + '</div>' +
        '</div>' +
        '<div>' +
        '<div class="section-title">2. FUNCTIONAL REQUIREMENTS & ACCEPTANCE CRITERIA (' + (spec.criteria ? spec.criteria.length : 0) + ' ITEMS)</div>' +
        criteriaHtml +
        '</div>' +
        '<div>' +
        '<div class="section-title">3. NON-FUNCTIONAL REQUIREMENTS (NFRs)</div>' +
        '<div class="row g-3">' +
        '<div class="col-6"><div class="nfr-card"><strong class="d-block mb-1 text-primary">🔒 Security & Authorization</strong>Role-based access control (RBAC), Entra ID MFA authentication, encrypted TLS 1.3 transit, and data-at-rest protection.</div></div>' +
        '<div class="col-6"><div class="nfr-card"><strong class="d-block mb-1 text-primary">⚡ Performance & Availability</strong>API response latency &lt; 200ms at 95th percentile. System SLA requirement of 99.9% uptime.</div></div>' +
        '<div class="col-6"><div class="nfr-card"><strong class="d-block mb-1 text-primary">📜 Auditability & Reliability</strong>Immutable audit trail for state mutations. Standardized RFC 7807 problem details error handling.</div></div>' +
        '<div class="col-6"><div class="nfr-card"><strong class="d-block mb-1 text-primary">🌐 Scalability & Integration</strong>Stateless RESTful architecture, containerized deployment, microservice domain isolation.</div></div>' +
        '</div>' +
        '</div>' +
        '<div>' +
        '<div class="section-title">4. SCOPE TAGS & TRACEABILITY</div>' +
        '<div class="d-flex gap-2 flex-wrap">' + tagsHtml + '</div>' +
        '</div>' +
        '<div class="mt-4">' +
        '<div class="section-title">5. STAKEHOLDER SIGN-OFF MATRIX</div>' +
        '<table class="table table-bordered mt-2 small" style="font-size:12px;">' +
        '<thead class="table-light"><tr><th>Role</th><th>Name</th><th>Signature</th><th>Date</th></tr></thead>' +
        '<tbody>' +
        '<tr><td>Product Owner / BA</td><td>____________________</td><td>____________________</td><td>____ / ____ / 2026</td></tr>' +
        '<tr><td>Lead Architect</td><td>____________________</td><td>____________________</td><td>____ / ____ / 2026</td></tr>' +
        '<tr><td>QA Engineering Lead</td><td>____________________</td><td>____________________</td><td>____ / ____ / 2026</td></tr>' +
        '</tbody></table>' +
        '</div>' +
        '</body></html>';

    printWindow.document.write(htmlContent);
    printWindow.document.close();
    setTimeout(function() {
        try { printWindow.print(); } catch(e){}
    }, 600);
};
