// Minimal Trading Dashboard - Chart.js Only
window.minimalTradingDashboard = {
    charts: {
        trading: null,
        equity: null
    },
    
    isInitialized: false,
    
    // Check if the dashboard is properly initialized
    isReady: function() {
        return this.isInitialized && typeof Chart !== 'undefined';
    },
    
    // Initialize charts only - no data fetching
    initialize: function() {
        console.log('📊 Initializing minimal charts (data from C#)...');
        
        return new Promise((resolve, reject) => {
            const checkChart = () => {
                if (typeof Chart !== 'undefined') {
                    console.log('✅ Chart.js available');
                    try {
                        this.initializeMainChart();
                        this.initializeEquityChart();
                        this.isInitialized = true;
                        resolve();
                    } catch (error) {
                        console.error('❌ Error initializing charts:', error);
                        reject(error);
                    }
                } else {
                    setTimeout(checkChart, 100);
                }
            };
            checkChart();
        });
    },

    // Initialize main trading chart
    initializeMainChart: function() {
        const canvas = document.getElementById('tradingChartCanvas');
        if (!canvas) {
            console.error('❌ Trading chart canvas not found');
            return;
        }

        if (this.charts.trading) {
            this.charts.trading.destroy();
        }

        this.charts.trading = new Chart(canvas, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: 'BTC Price',
                        data: [],
                        borderColor: '#f7931e',
                        backgroundColor: 'rgba(247, 147, 30, 0.1)',
                        borderWidth: 3,
                        fill: true,
                        tension: 0.2,
                        pointRadius: 0
                    },
                    {
                        label: 'Keltner Upper',
                        data: [],
                        borderColor: '#dc3545',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        pointRadius: 0,
                        fill: false
                    },
                    {
                        label: 'Keltner Middle',
                        data: [],
                        borderColor: '#ffc107',
                        borderWidth: 2,
                        pointRadius: 0,
                        fill: false
                    },
                    {
                        label: 'Keltner Lower',
                        data: [],
                        borderColor: '#28a745',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        pointRadius: 0,
                        fill: false
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    title: {
                        display: true,
                        text: 'BTC/USDT - Keltner Channel Strategy',
                        font: { size: 16, weight: 'bold' }
                    },
                    legend: { display: true, position: 'top' },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': $' + 
                                    context.parsed.y.toLocaleString('en-US', {
                                        minimumFractionDigits: 2,
                                        maximumFractionDigits: 2
                                    });
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        title: { display: true, text: 'Time' }
                    },
                    y: {
                        display: true,
                        title: { display: true, text: 'Price (USD)' },
                        ticks: {
                            callback: function(value) {
                                return '$' + value.toLocaleString('en-US');
                            }
                        }
                    }
                }
            }
        });
        
        console.log('✅ Main chart initialized');
    },

    // Initialize equity chart
    initializeEquityChart: function() {
        const canvas = document.getElementById('equityChartCanvas');
        if (!canvas) {
            console.error('❌ Equity chart canvas not found');
            return;
        }

        if (this.charts.equity) {
            this.charts.equity.destroy();
        }

        this.charts.equity = new Chart(canvas, {
            type: 'line',
            data: {
                labels: [],
                datasets: [{
                    label: 'Portfolio Value',
                    data: [],
                    borderColor: '#28a745',
                    backgroundColor: 'rgba(40, 167, 69, 0.1)',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { 
                        enabled: true,
                        callbacks: {
                            label: function(context) {
                                return 'Value: ' + context.parsed.y.toFixed(4) + ' BTC';
                            }
                        }
                    }
                },
                scales: {
                    x: { display: false },
                    y: { display: false, beginAtZero: false }
                }
            }
        });
        
        console.log('✅ Equity chart initialized');
    },

    // Update chart data - called from C# via JS interop
    updateChartData: function(chartData) {
        if (!this.isReady()) {
            console.warn('⚠️ Dashboard not ready for chart updates');
            return;
        }

        if (!this.charts.trading) {
            console.warn('⚠️ Chart not initialized yet');
            return;
        }

        // Update main chart
        this.charts.trading.data.labels = chartData.timestamps || [];
        this.charts.trading.data.datasets[0].data = chartData.prices || [];
        this.charts.trading.data.datasets[1].data = chartData.kcUpper || [];
        this.charts.trading.data.datasets[2].data = chartData.kcMiddle || [];
        this.charts.trading.data.datasets[3].data = chartData.kcLower || [];
        
        this.charts.trading.update('none'); // No animation for real-time updates
        
        console.log('📊 Chart updated with', chartData.prices?.length || 0, 'data points');
    },

    // Update equity chart - called from C#
    updateEquityChart: function(equityData) {
        if (!this.isReady()) {
            console.warn('⚠️ Dashboard not ready for equity chart updates');
            return;
        }

        if (!this.charts.equity) {
            console.warn('⚠️ Equity chart not initialized yet');
            return;
        }

        this.charts.equity.data.labels = equityData.labels || [];
        this.charts.equity.data.datasets[0].data = equityData.values || [];
        this.charts.equity.update('none');
    },

    // Add single data point for real-time updates
    addDataPoint: function(timestamp, price, kcUpper, kcMiddle, kcLower) {
        if (!this.isReady() || !this.charts.trading) return;

        const maxPoints = 100;
        
        // Add new data
        this.charts.trading.data.labels.push(timestamp);
        this.charts.trading.data.datasets[0].data.push(price);
        this.charts.trading.data.datasets[1].data.push(kcUpper);
        this.charts.trading.data.datasets[2].data.push(kcMiddle);
        this.charts.trading.data.datasets[3].data.push(kcLower);
        
        // Keep only last maxPoints
        if (this.charts.trading.data.labels.length > maxPoints) {
            this.charts.trading.data.labels.shift();
            this.charts.trading.data.datasets.forEach(dataset => {
                dataset.data.shift();
            });
        }
        
        this.charts.trading.update('none');
    },

    // Destroy charts
    dispose: function() {
        try {
            if (this.charts.trading) {
                this.charts.trading.destroy();
                this.charts.trading = null;
            }
            if (this.charts.equity) {
                this.charts.equity.destroy();
                this.charts.equity = null;
            }
            this.isInitialized = false;
            console.log('🧹 Charts disposed');
        } catch (error) {
            console.error('❌ Error disposing charts:', error);
        }
    }
}; 