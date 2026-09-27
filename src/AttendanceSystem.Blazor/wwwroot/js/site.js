window.scrollElementToBottom = (elementId) => {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
};

window.chatHistoryStorage = {
    load: (key) => {
        try {
            return localStorage.getItem(key);
        } catch (error) {
            console.warn('Failed to load chat history:', error);
            return null;
        }
    },
    save: (key, value) => {
        try {
            localStorage.setItem(key, value);
        } catch (error) {
            console.warn('Failed to save chat history:', error);
        }
    },
    clear: (key) => {
        try {
            localStorage.removeItem(key);
        } catch (error) {
            console.warn('Failed to clear chat history:', error);
        }
    }
};

window.renderDonutChart = (canvasId, labelsOrValues, valuesOrColors, maybeColors) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) return;
    const ctx = canvas.getContext('2d');
    if (canvas._chartInstance) canvas._chartInstance.destroy();

    const hasLabels = Array.isArray(maybeColors);
    const labels = hasLabels ? labelsOrValues : [];
    const values = hasLabels ? valuesOrColors : labelsOrValues;
    const colors = hasLabels ? maybeColors : valuesOrColors;
    const total = values.reduce((a, b) => a + b, 0);

    // Center-text plugin
    const centerPlugin = {
        id: 'centerText',
        afterDraw(chart) {
            const { ctx: c, chartArea: { width, height, top, left } } = chart;
            c.save();
            const cx = left + width / 2;
            const cy = top + height / 2;
            c.textAlign = 'center';
            c.textBaseline = 'middle';
            c.font = 'bold 28px Inter, system-ui, sans-serif';
            c.fillStyle = getComputedStyle(document.documentElement).getPropertyValue('--clr-text').trim() || '#0F172A';
            c.fillText(total, cx, cy - 10);
            c.font = '12px Inter, system-ui, sans-serif';
            c.fillStyle = getComputedStyle(document.documentElement).getPropertyValue('--clr-text-muted').trim() || '#64748B';
            c.fillText('нийт', cx, cy + 14);
            c.restore();
        }
    };

    canvas._chartInstance = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels,
            datasets: [{
                data: values,
                backgroundColor: colors,
                borderWidth: 3,
                borderColor: getComputedStyle(document.documentElement).getPropertyValue('--clr-surface').trim() || '#fff',
                hoverBorderWidth: 3,
                hoverOffset: 6
            }]
        },
        options: {
            cutout: '68%',
            plugins: {
                legend: { display: false },
                tooltip: {
                    enabled: true,
                    callbacks: {
                        label: (ctx) => {
                            const pct = total > 0 ? Math.round(ctx.parsed / total * 100) : 0;
                            return ` ${ctx.parsed} хүн (${pct}%)`;
                        }
                    }
                }
            },
            animation: { duration: 700, easing: 'easeInOutQuart' }
        },
        plugins: [centerPlugin]
    });
};

window.downloadBase64File = (base64, fileName, mimeType) => {
    if (!base64) return;

    const cleanedBase64 = base64.replace(/\s/g, '');
    const binary = atob(cleanedBase64);
    const bytes = new Uint8Array(binary.length);

    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    link.href = url;
    link.download = fileName || 'download';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
};
