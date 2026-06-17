window.scrollElementToBottom = (elementId) => {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
};

window.renderDonutChart = (canvasId, values, colors) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (canvas._chartInstance) canvas._chartInstance.destroy();
    canvas._chartInstance = new Chart(ctx, {
        type: 'doughnut',
        data: {
            datasets: [{
                data: values,
                backgroundColor: colors,
                borderWidth: 0,
                hoverOffset: 4
            }]
        },
        options: {
            cutout: '72%',
            plugins: { legend: { display: false }, tooltip: { enabled: true } },
            animation: { duration: 600 }
        }
    });
};