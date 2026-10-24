// Gestionnaire de colonnes réordonnables (Drag and Drop), masquables et persistantes
(function () {
    window.TableColumns = {
        init: function (options) {
            const tableId = options.tableId || 'transactionsTable';
            const tableKey = options.tableKey || 'Transactions';
            const serverConfigJson = options.serverConfigJson || null;
            const saveUrl = options.saveUrl || (window.location.pathname + '?handler=SaveColumnsConfig');

            const table = document.getElementById(tableId);
            if (!table) return;

            const thead = table.querySelector('thead');
            const tbody = table.querySelector('tbody');
            if (!thead || !tbody) return;

            // Définition de base des colonnes disponibles
            const thElements = Array.from(thead.querySelectorAll('tr:first-child th'));
            const defaultOrder = [];
            const colDefinitions = {};

            thElements.forEach((th, index) => {
                const colKey = th.getAttribute('data-col-key');
                if (!colKey) return;
                const colLabel = th.getAttribute('data-col-label') || th.innerText.trim();
                const canHide = th.getAttribute('data-col-hideable') !== 'false';
                const canReorder = th.getAttribute('data-col-reorderable') !== 'false';

                defaultOrder.push(colKey);
                colDefinitions[colKey] = {
                    key: colKey,
                    label: colLabel,
                    canHide: canHide,
                    canReorder: canReorder,
                    defaultIndex: index
                };
            });

            // Récupérer la configuration enregistrée (Base de données puis localStorage)
            let config = null;
            if (serverConfigJson) {
                try {
                    config = JSON.parse(serverConfigJson);
                } catch (e) {
                    console.warn('Erreur parsing config colonnes serveur:', e);
                }
            }
            if (!config) {
                try {
                    const localSaved = localStorage.getItem('cols_' + tableKey);
                    if (localSaved) config = JSON.parse(localSaved);
                } catch (e) {}
            }

            // Normaliser la config
            if (!config || !Array.isArray(config.order)) {
                config = {
                    order: [...defaultOrder],
                    hidden: []
                };
            } else {
                // S'assurer que toutes les colonnes existantes sont présentes
                defaultOrder.forEach(k => {
                    if (!config.order.includes(k)) config.order.push(k);
                });
                // Nettoyer les colonnes qui n'existeraient plus
                config.order = config.order.filter(k => defaultOrder.includes(k));
                if (!Array.isArray(config.hidden)) config.hidden = [];
            }

            // Fonction pour appliquer la visibilité et l'ordre
            function applyConfig() {
                // 1. Visibilité CSS via classes ou styles
                defaultOrder.forEach(colKey => {
                    const isHidden = config.hidden.includes(colKey);
                    const cells = table.querySelectorAll(`[data-col-key="${colKey}"]`);
                    cells.forEach(cell => {
                        if (isHidden) {
                            cell.classList.add('d-none');
                        } else {
                            cell.classList.remove('d-none');
                        }
                    });
                });

                // 2. Réordonner les colonnes dans thead et tbody
                const theadRow = thead.querySelector('tr');
                if (theadRow) {
                    config.order.forEach(colKey => {
                        const th = theadRow.querySelector(`th[data-col-key="${colKey}"]`);
                        if (th) theadRow.appendChild(th);
                    });
                }

                const rows = tbody.querySelectorAll('tr');
                rows.forEach(tr => {
                    // Ne pas toucher aux lignes de type "Aucun résultat" avec colspan
                    if (tr.querySelector('td[colspan]')) return;

                    config.order.forEach(colKey => {
                        const td = tr.querySelector(`td[data-col-key="${colKey}"]`);
                        if (td) tr.appendChild(td);
                    });
                });

                // Mettre à jour l'état des cases à cocher dans le menu déroulant
                renderDropdownMenu();
            }

            // Sauvegarde de la configuration
            function saveConfig() {
                const configJson = JSON.stringify(config);
                // Sauvegarde locale
                try {
                    localStorage.setItem('cols_' + tableKey, configJson);
                } catch (e) {}

                // Sauvegarde en base de données
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                fetch(saveUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token,
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({
                        tableKey: tableKey,
                        configJson: configJson
                    })
                }).catch(err => {
                    console.warn('Erreur sauvegarde colonnes serveur:', err);
                });
            }

            // Réinitialisation par défaut
            function resetToDefault() {
                config = {
                    order: [...defaultOrder],
                    hidden: []
                };
                applyConfig();
                saveConfig();
            }

            // Rendu du menu de personnalisation des colonnes
            function renderDropdownMenu() {
                const menuContainer = document.getElementById('columnsDropdownMenu');
                if (!menuContainer) return;

                let html = `
                    <li class="dropdown-header d-flex justify-content-between align-items-center py-1">
                        <span class="fw-bold small text-uppercase" style="font-size: 0.72rem;">Afficher / Masquer</span>
                    </li>
                `;

                defaultOrder.forEach(colKey => {
                    const def = colDefinitions[colKey];
                    if (!def || !def.canHide) return;

                    const isChecked = !config.hidden.includes(colKey);
                    html += `
                        <li>
                            <label class="dropdown-item d-flex align-items-center gap-2 py-1 small cursor-pointer">
                                <input type="checkbox" class="form-check-input col-toggle-check my-0" 
                                       data-col-toggle="${colKey}" ${isChecked ? 'checked' : ''} />
                                <span>${def.label}</span>
                            </label>
                        </li>
                    `;
                });

                html += `
                    <li><hr class="dropdown-divider my-1"></li>
                    <li class="px-2 py-1 text-muted small" style="font-size: 0.72rem;">
                        <i class="bi bi-arrows-move me-1"></i> Glissez les en-têtes pour les réordonner
                    </li>
                    <li>
                        <button type="button" class="dropdown-item small text-danger d-flex align-items-center gap-2 py-1" id="btnResetColumns">
                            <i class="bi bi-arrow-counterclockwise"></i> Rétablir l'ordre par défaut
                        </button>
                    </li>
                `;

                menuContainer.innerHTML = html;

                // Bind des événements
                menuContainer.querySelectorAll('.col-toggle-check').forEach(chk => {
                    chk.addEventListener('change', function (e) {
                        e.stopPropagation();
                        const key = this.getAttribute('data-col-toggle');
                        if (this.checked) {
                            config.hidden = config.hidden.filter(k => k !== key);
                        } else {
                            // Empêcher de masquer toutes les colonnes
                            const visibleCount = defaultOrder.filter(k => !config.hidden.includes(k) && colDefinitions[k]?.canHide).length;
                            if (visibleCount <= 1) {
                                this.checked = true;
                                alert('Vous devez conserver au moins une colonne visible.');
                                return;
                            }
                            if (!config.hidden.includes(key)) config.hidden.push(key);
                        }
                        applyConfig();
                        saveConfig();
                    });
                });

                const btnReset = document.getElementById('btnResetColumns');
                if (btnReset) {
                    btnReset.addEventListener('click', function (e) {
                        e.preventDefault();
                        resetToDefault();
                    });
                }
            }

            // Gestion du Glisser-Déposer (HTML5 Drag & Drop)
            let draggedColKey = null;

            thElements.forEach(th => {
                const colKey = th.getAttribute('data-col-key');
                const canReorder = th.getAttribute('data-col-reorderable') !== 'false';

                if (canReorder) {
                    th.setAttribute('draggable', 'true');
                    th.classList.add('col-draggable');

                    th.addEventListener('dragstart', function (e) {
                        draggedColKey = colKey;
                        e.dataTransfer.effectAllowed = 'move';
                        e.dataTransfer.setData('text/plain', colKey);
                        th.classList.add('col-dragging');
                    });

                    th.addEventListener('dragend', function () {
                        draggedColKey = null;
                        th.classList.remove('col-dragging');
                        document.querySelectorAll('.col-drag-over-left, .col-drag-over-right').forEach(el => {
                            el.classList.remove('col-drag-over-left', 'col-drag-over-right');
                        });
                    });

                    th.addEventListener('dragover', function (e) {
                        e.preventDefault();
                        if (!draggedColKey || draggedColKey === colKey) return;
                        e.dataTransfer.dropEffect = 'move';

                        const rect = th.getBoundingClientRect();
                        const isRight = (e.clientX - rect.left) > (rect.width / 2);

                        if (isRight) {
                            th.classList.remove('col-drag-over-left');
                            th.classList.add('col-drag-over-right');
                        } else {
                            th.classList.remove('col-drag-over-right');
                            th.classList.add('col-drag-over-left');
                        }
                    });

                    th.addEventListener('dragleave', function () {
                        th.classList.remove('col-drag-over-left', 'col-drag-over-right');
                    });

                    th.addEventListener('drop', function (e) {
                        e.preventDefault();
                        th.classList.remove('col-drag-over-left', 'col-drag-over-right');
                        if (!draggedColKey || draggedColKey === colKey) return;

                        const rect = th.getBoundingClientRect();
                        const dropAfter = (e.clientX - rect.left) > (rect.width / 2);

                        // Modifier l'ordre dans config.order
                        const currentOrder = [...config.order];
                        const fromIdx = currentOrder.indexOf(draggedColKey);
                        currentOrder.splice(fromIdx, 1);

                        let toIdx = currentOrder.indexOf(colKey);
                        if (dropAfter) {
                            toIdx++;
                        }
                        currentOrder.splice(toIdx, 0, draggedColKey);

                        config.order = currentOrder;
                        applyConfig();
                        saveConfig();
                    });
                }
            });

            // Appliquer au chargement
            applyConfig();
        }
    };
})();
