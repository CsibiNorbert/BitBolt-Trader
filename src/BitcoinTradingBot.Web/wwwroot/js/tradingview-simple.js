// TradingView Lightweight Charts - Clean Implementation
// Based on official TradingView Lightweight Charts documentation
// Free and open source under Apache 2.0 license

console.log('TradingView Simple JS loading...');

// Ensure global object exists immediately
window.TradingViewChart = {
    charts: new Map(),
    
    createChart: function(containerId, options = {}) {
        console.log('Creating chart for container:', containerId);
        
        return new Promise((resolve, reject) => {
            try {
                // Check if container exists
                const container = document.getElementById(containerId);
                if (!container) {
                    const error = `Container with id '${containerId}' not found`;
                    console.error(error);
                    reject(new Error(error));
                    return;
                }
                
                // Check if LightweightCharts is available
                if (typeof LightweightCharts === 'undefined') {
                    const error = 'LightweightCharts library not loaded';
                    console.error(error);
                    reject(new Error(error));
                    return;
                }
                
                console.log('Container found, creating chart...');
                
                // Default chart options
                const chartOptions = {
                    width: container.clientWidth || 800,
                    height: container.clientHeight || 400,
                    layout: {
                        background: { color: '#1e1e1e' },
                        textColor: '#d1d4dc',
                    },
                    grid: {
                        vertLines: { color: '#2b2b43' },
                        horzLines: { color: '#2b2b43' },
                    },
                    crosshair: {
                        mode: LightweightCharts.CrosshairMode.Normal,
                    },
                    rightPriceScale: {
                        borderColor: '#485c7b',
                    },
                    timeScale: {
                        borderColor: '#485c7b',
                        timeVisible: true,
                        secondsVisible: false,
                    },
                    ...options
                };
                
                // Create the chart
                const chart = LightweightCharts.createChart(container, chartOptions);
                
                // Store chart reference
                this.charts.set(containerId, chart);
                
                console.log('Chart created successfully');
                resolve(chart);
                
            } catch (error) {
                console.error('Error creating chart:', error);
                reject(error);
            }
        });
    },
    
    addCandlestickSeries: function(containerId, seriesOptions = {}) {
        console.log('Adding candlestick series to:', containerId);
        
        const chart = this.charts.get(containerId);
        if (!chart) {
            throw new Error(`Chart not found for container: ${containerId}`);
        }
        
        const defaultOptions = {
            upColor: '#26a69a',
            downColor: '#ef5350',
            borderVisible: false,
            wickUpColor: '#26a69a',
            wickDownColor: '#ef5350',
        };
        
        const series = chart.addCandlestickSeries({
            ...defaultOptions,
            ...seriesOptions
        });
        
        console.log('Candlestick series added');
        return series;
    },
    
    setData: function(series, data) {
        console.log('Setting data, count:', data.length);
        if (series && data && data.length > 0) {
            series.setData(data);
            console.log('Data set successfully');
        } else {
            console.warn('Invalid series or data');
        }
    },
    
    generateSampleData: function() {
        console.log('Generating sample data...');
        
        const data = [];
        const basePrice = 50000; // Bitcoin base price
        let currentPrice = basePrice;
        
        // Generate 100 data points
        for (let i = 0; i < 100; i++) {
            const time = Math.floor(Date.now() / 1000) - (100 - i) * 3600; // 1 hour intervals
            
            // Simple random walk
            const change = (Math.random() - 0.5) * 1000; // Random change up to $500
            currentPrice += change;
            
            const high = currentPrice + Math.random() * 200;
            const low = currentPrice - Math.random() * 200;
            const open = currentPrice - change / 2;
            const close = currentPrice;
            
            data.push({
                time: time,
                open: Math.round(open * 100) / 100,
                high: Math.round(high * 100) / 100,
                low: Math.round(low * 100) / 100,
                close: Math.round(close * 100) / 100,
            });
        }
        
        console.log('Sample data generated:', data.length, 'points');
        return data;
    },
    
    resizeChart: function(containerId) {
        const chart = this.charts.get(containerId);
        const container = document.getElementById(containerId);
        
        if (chart && container) {
            chart.applyOptions({
                width: container.clientWidth,
                height: container.clientHeight,
            });
            console.log('Chart resized');
        }
    },
    
    removeChart: function(containerId) {
        const chart = this.charts.get(containerId);
        if (chart) {
            chart.remove();
            this.charts.delete(containerId);
            console.log('Chart removed:', containerId);
        }
    }
};

console.log('TradingViewChart object initialized:', window.TradingViewChart); 