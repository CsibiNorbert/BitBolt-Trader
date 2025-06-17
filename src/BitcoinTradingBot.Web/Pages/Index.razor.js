// ES6 module for Blazor JS isolation

// We no longer need a global object.
// We export the functions that the Blazor component will call.

let charts = new Map();

// Exported function to initialize the chart
export function initializeTradingViewChart(containerId, data) {
    try {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error(`Container with id '${containerId}' not found`);
            return;
        }

        if (typeof LightweightCharts === 'undefined') {
            console.error('LightweightCharts library not loaded');
            return;
        }

        // Remove any existing chart before creating a new one
        if (charts.has(containerId)) {
            charts.get(containerId).remove();
            charts.delete(containerId);
        }

        const chartOptions = {
            width: container.clientWidth,
            height: container.clientHeight || 500, // Ensure a default height
            layout: {
                background: { type: 'solid', color: '#ffffff' },
                textColor: '#333333',
            },
            grid: {
                vertLines: { color: '#e1ecf2' },
                horzLines: { color: '#e1ecf2' },
            },
            crosshair: {
                mode: LightweightCharts.CrosshairMode.Normal,
            },
            rightPriceScale: {
                borderColor: '#cccccc',
            },
            timeScale: {
                borderColor: '#cccccc',
                timeVisible: true,
                secondsVisible: false,
            }
        };

        const chart = LightweightCharts.createChart(container, chartOptions);
        const candleSeries = chart.addCandlestickSeries({
            upColor: '#26a69a',
            downColor: '#ef5350',
            borderVisible: false,
            wickUpColor: '#26a69a',
            wickDownColor: '#ef5350',
        });

        candleSeries.setData(data);
        charts.set(containerId, { chart, candleSeries });
        
        // Auto-resize chart on window resize
        const resizeObserver = new ResizeObserver(entries => {
            const { width, height } = entries[0].contentRect;
            chart.applyOptions({ width, height });
        });
        resizeObserver.observe(container);

    } catch (error) {
        console.error('Error creating chart:', error);
    }
}

// Exported function to update the chart with a new candlestick
export function updateTradingViewChart(containerId, newCandle) {
    const chartInstance = charts.get(containerId);
    if (chartInstance && chartInstance.candleSeries) {
        chartInstance.candleSeries.update(newCandle);
    }
}

// Exported function to clean up resources
export function disposeChart(containerId) {
    const chartInstance = charts.get(containerId);
    if (chartInstance) {
        chartInstance.chart.remove();
        charts.delete(containerId);
        console.log(`Chart ${containerId} disposed.`);
    }
} 