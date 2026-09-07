// Department chart rendering for reports
window.renderDepartmentChart = function(canvasId, labels, presentData, lateData, absentData, leaveData) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    // Destroy existing chart if it exists
    if (canvas.chart) {
        canvas.chart.destroy();
    }

    const ctx = canvas.getContext('2d');
    
    canvas.chart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'Ирсэн',
                    data: presentData,
                    backgroundColor: 'rgba(22, 163, 74, 0.8)',
                    borderColor: 'rgba(22, 163, 74, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Хоцорсон',
                    data: lateData,
                    backgroundColor: 'rgba(217, 119, 6, 0.8)',
                    borderColor: 'rgba(217, 119, 6, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Ирээгүй',
                    data: absentData,
                    backgroundColor: 'rgba(220, 38, 38, 0.8)',
                    borderColor: 'rgba(220, 38, 38, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Чөлөөтэй',
                    data: leaveData,
                    backgroundColor: 'rgba(124, 58, 237, 0.8)',
                    borderColor: 'rgba(124, 58, 237, 1)',
                    borderWidth: 1
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false // Using custom legend
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            if (context.parsed.y !== null) {
                                label += context.parsed.y;
                            }
                            return label;
                        }
                    }
                }
            },
            scales: {
                x: {
                    stacked: true,
                    grid: {
                        display: false
                    }
                },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)'
                    }
                }
            }
        }
    });
};

// Overtime trend chart
window.renderOvertimeChart = function(canvasId, labels, overtimeData) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    // Destroy existing chart if it exists
    if (canvas.chart) {
        canvas.chart.destroy();
    }

    const ctx = canvas.getContext('2d');
    
    canvas.chart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Илүү цаг',
                data: overtimeData,
                borderColor: 'rgba(99, 102, 241, 1)',
                backgroundColor: 'rgba(99, 102, 241, 0.1)',
                borderWidth: 2,
                fill: true,
                tension: 0.4,
                pointRadius: 4,
                pointHoverRadius: 6
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            return `Илүү цаг: ${context.parsed.y.toFixed(2)}ц`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: {
                        display: false
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)'
                    },
                    ticks: {
                        callback: function(value) {
                            return value + 'ц';
                        }
                    }
                }
            }
        }
    });
};