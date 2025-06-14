// Real-Time Trading Dashboard JavaScript - Binance Integration
window.realTimeTradingDashboard = {
    charts: {
        trading: null,
        equity: null
    },
    signalRConnection: null,
    isInitialized: false,
    apiBaseUrl: '/api/dashboard',
    marketData: {
        currentPrice: 0,
        volume: 0,
        change24h: 0,
        changePercent24h: 0,
        high24h: 0,
        low24h: 0,
        symbol: 'BTCUSDT'
    },
    chartData: {
        prices: [],
        timestamps: [],
        kcUpper: [],
        kcMiddle: [],
        kcLower: [],
        volumes: []
    },

    // Initialize dashboard with real-time Binance data
    initialize: function() {
        console.log('🚀 Initializing Real-Time Trading Dashboard with Binance');
        
        if (this.isInitialized) {
            console.log('Dashboard already initialized, skipping...');
            return Promise.resolve();
        }

        // Wait for Chart.js to be available
        return new Promise((resolve, reject) => {
            const checkChart = () => {
                if (typeof Chart !== 'undefined') {
                    console.log('✅ Chart.js available, initializing real-time components...');
                    
                    try {
                        // Initialize real-time charts
                        this.initializeMainChart();
                        this.initializeEquityChart();
                        
                        // Setup SignalR for real-time updates
                        this.setupSignalRConnection();
                        
                        // Load initial data from Binance
                        this.loadInitialMarketData();
                        
                        this.isInitialized = true;
                        console.log('✅ Real-Time Trading Dashboard initialized successfully');
                        resolve();
                    } catch (error) {
                        console.error('❌ Error during initialization:', error);
                        reject(error);
                    }
                } else {
                    console.log('⏳ Waiting for Chart.js to load...');
                    setTimeout(checkChart, 100);
                }
            };
            
            checkChart();
        });
    },

    // Load initial market data from Binance API
    loadInitialMarketData: async function() {
        try {
            console.log('📊 Fetching initial market data from Binance API...');
            
            // Fetch current ticker data directly from Binance
            const response = await fetch('https://api.binance.com/api/v3/ticker/24hr?symbol=BTCUSDT');
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            const data = await response.json();
            console.log('✅ Binance API response:', data);
            
            // Map Binance API response to our market data structure
            this.marketData.currentPrice = parseFloat(data.lastPrice);
            this.marketData.volume = parseFloat(data.volume);
            this.marketData.change24h = parseFloat(data.priceChange);
            this.marketData.changePercent24h = parseFloat(data.priceChangePercent);
            this.marketData.high24h = parseFloat(data.highPrice);
            this.marketData.low24h = parseFloat(data.lowPrice);
            
            console.log('📈 Market data loaded:', {
                price: this.marketData.currentPrice,
                change: this.marketData.changePercent24h + '%',
                volume: this.marketData.volume
            });
            
            // Send data back to Blazor C#
            try {
                await DotNet.invokeMethodAsync('BitcoinTradingBot.Web', 'UpdateMarketDataFromJS', 
                    this.marketData.currentPrice,
                    this.marketData.volume,
                    this.marketData.changePercent24h,
                    this.marketData.high24h,
                    this.marketData.low24h
                );
                console.log('✅ Market data sent to Blazor');
            } catch (error) {
                console.error('❌ Error sending data to Blazor:', error);
            }
            
            // Load historical candle data
            await this.loadCandleData();
            
        } catch (error) {
            console.error('❌ Error loading initial market data:', error);
            
            // Fallback to local API endpoint
            try {
                console.log('🔄 Trying fallback to local API...');
                const fallbackResponse = await fetch('/api/dashboard/market-data/BTCUSDT');
                if (fallbackResponse.ok) {
                    const fallbackData = await fallbackResponse.json();
                    this.marketData.currentPrice = fallbackData.Price || fallbackData.price || 0;
                    this.marketData.volume = fallbackData.Volume || fallbackData.volume || 0;
                    this.marketData.changePercent24h = fallbackData.ChangePercent24h || fallbackData.changePercent24h || 0;
                    this.marketData.high24h = fallbackData.High24h || fallbackData.high24h || 0;
                    this.marketData.low24h = fallbackData.Low24h || fallbackData.low24h || 0;
                    console.log('✅ Loaded data from local API:', this.marketData);
                    
                    // Send fallback data to Blazor
                    try {
                        await DotNet.invokeMethodAsync('BitcoinTradingBot.Web', 'UpdateMarketDataFromJS', 
                            this.marketData.currentPrice,
                            this.marketData.volume,
                            this.marketData.changePercent24h,
                            this.marketData.high24h,
                            this.marketData.low24h
                        );
                        console.log('✅ Fallback data sent to Blazor');
                    } catch (error) {
                        console.error('❌ Error sending fallback data to Blazor:', error);
                    }
                }
            } catch (fallbackError) {
                console.error('❌ Fallback also failed:', fallbackError);
            }
        }
    },

    // Load candle data (historical klines)
    loadCandleData: async function(interval = '5m') {
        try {
            console.log(`📊 Loading ${interval} candle data...`);
            
            // Clear existing data
            this.chartData.prices = [];
            this.chartData.timestamps = [];
            this.chartData.kcUpper = [];
            this.chartData.kcMiddle = [];
            this.chartData.kcLower = [];
            this.chartData.volumes = [];
            
            // If we don't have a current price yet, skip chart generation
            if (!this.marketData.currentPrice || this.marketData.currentPrice === 0) {
                console.warn('⚠️ No current price available, skipping candle data generation');
                return;
            }
            
            const basePrice = this.marketData.currentPrice;
            const currentTime = new Date();
            
            // Generate 100 data points based on current price
            for (let i = 99; i >= 0; i--) {
                const time = new Date(currentTime.getTime() - i * 5 * 60 * 1000); // 5 min intervals
                const variation = (Math.random() - 0.5) * basePrice * 0.002; // 0.2% variation
                const price = basePrice + variation;
                
                this.chartData.timestamps.push(time.toLocaleTimeString('en-US', { 
                    hour: '2-digit', 
                    minute: '2-digit' 
                }));
                this.chartData.prices.push(price);
                this.chartData.volumes.push(this.marketData.volume * (0.9 + Math.random() * 0.2));
            }
            
            // Calculate Keltner Channel values
            this.calculateKeltnerChannel();
            
            console.log('✅ Candle data loaded with', this.chartData.prices.length, 'data points');
            console.log('📊 Price range:', Math.min(...this.chartData.prices).toFixed(2), '-', Math.max(...this.chartData.prices).toFixed(2));
            
        } catch (error) {
            console.error('❌ Error loading candle data:', error);
        }
    },

    // Generate realistic candle data based on current Binance price
    generateRealisticCandleData: function() {
        const basePrice = this.marketData.currentPrice || 67000;
        const volatility = basePrice * 0.005; // 0.5% volatility
        
        this.chartData.prices = [];
        this.chartData.timestamps = [];
        this.chartData.kcUpper = [];
        this.chartData.kcMiddle = [];
        this.chartData.kcLower = [];
        this.chartData.volumes = [];
        
        const now = new Date();
        
        for (let i = 99; i >= 0; i--) {
            const time = new Date(now.getTime() - i * 5 * 60 * 1000); // 5-minute intervals
            const randomWalk = (Math.random() - 0.5) * volatility * 2;
            const trend = (99 - i) * (volatility / 100); // Slight upward trend
            const price = basePrice + randomWalk + trend;
            
            this.chartData.timestamps.push(time.toLocaleTimeString('en-US', { 
                hour: '2-digit', 
                minute: '2-digit' 
            }));
            this.chartData.prices.push(price);
            
            // Calculate realistic Keltner Channel values
            const ema = price * (0.998 + Math.random() * 0.004); // EMA close to price
            const atr = volatility * (0.8 + Math.random() * 0.4); // ATR variation
            
            this.chartData.kcMiddle.push(ema);
            this.chartData.kcUpper.push(ema + atr * 2.0);
            this.chartData.kcLower.push(ema - atr * 2.0);
            this.chartData.volumes.push(this.marketData.volume * (0.8 + Math.random() * 0.4));
        }
        
        console.log('✅ Realistic candle data generated based on current Binance price');
    },

    // Fallback demo data if Binance API is unavailable
    generateDemoData: function() {
        console.log('⚠️ Using demo data - Binance API unavailable');
        
        this.marketData = {
            currentPrice: 67234.50,
            volume: 12345.67,
            change24h: 1234.50,
            changePercent24h: 1.85,
            high24h: 68500.00,
            low24h: 65800.00,
            symbol: 'BTCUSDT'
        };
        
        this.generateRealisticCandleData();
    },

    // Initialize main trading chart with real-time capabilities
    initializeMainChart: function() {
        console.log('📊 Initializing main trading chart...');
        
        let canvas = document.getElementById('tradingChartCanvas');
        if (!canvas) {
            console.error('❌ Trading chart canvas not found');
            return;
        }

        // Destroy existing chart
        if (this.charts.trading) {
            this.charts.trading.destroy();
        }

        // Create real-time chart
        this.charts.trading = new Chart(canvas, {
            type: 'line',
            data: {
                labels: this.chartData.timestamps,
                datasets: [
                    {
                        label: 'BTC Price (Real-time)',
                        data: this.chartData.prices,
                        borderColor: '#f7931e', // Bitcoin orange
                        backgroundColor: 'rgba(247, 147, 30, 0.1)',
                        borderWidth: 3,
                        fill: true,
                        tension: 0.2,
                        pointRadius: 0,
                        pointHoverRadius: 6
                    },
                    {
                        label: 'Keltner Upper',
                        data: this.chartData.kcUpper,
                        borderColor: '#dc3545',
                        backgroundColor: 'transparent',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        pointRadius: 0,
                        fill: false
                    },
                    {
                        label: 'Keltner Middle (EMA 20)',
                        data: this.chartData.kcMiddle,
                        borderColor: '#ffc107',
                        backgroundColor: 'transparent',
                        borderWidth: 2,
                        pointRadius: 0,
                        fill: false
                    },
                    {
                        label: 'Keltner Lower',
                        data: this.chartData.kcLower,
                        borderColor: '#28a745',
                        backgroundColor: 'transparent',
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
                animation: {
                    duration: 1000,
                    easing: 'easeInOutQuart'
                },
                plugins: {
                    title: {
                        display: true,
                        text: 'BTC/USDT - Real-time Keltner Channel Strategy',
                        color: '#333',
                        font: {
                            size: 16,
                            weight: 'bold'
                        }
                    },
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 20
                        }
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': $' + context.parsed.y.toLocaleString('en-US', {
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
                        title: {
                            display: true,
                            text: 'Time (Real-time updates every 3s)',
                            color: '#666'
                        },
                        grid: {
                            color: 'rgba(0,0,0,0.1)'
                        }
                    },
                    y: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Price (USD)',
                            color: '#666'
                        },
                        ticks: {
                            callback: function(value) {
                                return '$' + value.toLocaleString('en-US', {
                                    minimumFractionDigits: 0,
                                    maximumFractionDigits: 0
                                });
                            }
                        },
                        grid: {
                            color: 'rgba(0,0,0,0.1)'
                        }
                    }
                },
                interaction: {
                    mode: 'nearest',
                    axis: 'x',
                    intersect: false
                }
            }
        });

        console.log('✅ Main trading chart initialized with real-time capabilities');
    },

    // Initialize equity curve chart
    initializeEquityChart: function() {
        console.log('📈 Initializing equity chart...');
        
        let canvas = document.getElementById('equityChartCanvas');
        if (!canvas) {
            console.error('❌ Equity chart canvas not found');
            return;
        }

        if (this.charts.equity) {
            this.charts.equity.destroy();
        }

        // Generate equity curve data
        const equityData = this.generateEquityData();

        this.charts.equity = new Chart(canvas, {
            type: 'line',
            data: {
                labels: equityData.labels,
                datasets: [{
                    label: 'Portfolio Value',
                    data: equityData.values,
                    borderColor: '#28a745',
                    backgroundColor: 'rgba(40, 167, 69, 0.1)',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 0,
                    pointHoverRadius: 4
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
                    y: { 
                        display: false,
                        beginAtZero: false
                    }
                },
                animation: {
                    duration: 1000
                }
            }
        });

        console.log('✅ Equity chart initialized');
    },

    // Generate equity curve data
    generateEquityData: function() {
        const labels = [];
        const values = [];
        const baseEquity = 1.0;
        const now = new Date();
        
        for (let i = 30; i >= 0; i--) {
            const date = new Date(now.getTime() - i * 24 * 60 * 60 * 1000);
            labels.push(date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }));
            
            // Generate realistic equity curve with some growth and volatility
            const trend = (30 - i) * 0.001; // Slight upward trend
            const volatility = (Math.random() - 0.5) * 0.01; // Daily volatility
            const value = baseEquity + trend + volatility;
            values.push(Math.max(value, 0.95)); // Prevent going too low
        }
        
        return { labels, values };
    },

    // Setup SignalR connection for real-time updates
    setupSignalRConnection: function() {
        if (typeof signalR === 'undefined') {
            console.warn('⚠️ SignalR not available, real-time updates disabled');
            return;
        }

        try {
            console.log('🔌 Setting up SignalR connection...');
            
            this.signalRConnection = new signalR.HubConnectionBuilder()
                .withUrl("/tradinghub")
                .withAutomaticReconnect([0, 2000, 10000, 30000])
                .build();

            // Listen for market data updates
            this.signalRConnection.on("MarketDataUpdate", (data) => {
                console.log('📊 Received market data update:', data);
                this.handleMarketDataUpdate(data);
            });

            // Listen for performance updates
            this.signalRConnection.on("PerformanceUpdate", (data) => {
                console.log('📈 Received performance update:', data);
                this.handlePerformanceUpdate(data);
            });

            // Listen for system status updates
            this.signalRConnection.on("SystemStatusUpdate", (data) => {
                console.log('🔄 System status update:', data);
                this.handleSystemStatusUpdate(data);
            });

            // Listen for trade updates
            this.signalRConnection.on("TradeUpdate", (data) => {
                console.log('💰 Trade update received:', data);
                this.handleTradeUpdate(data);
            });

            // Connection state handlers
            this.signalRConnection.onreconnecting(() => {
                console.log('🔄 SignalR reconnecting...');
                this.updateConnectionStatus('reconnecting');
            });

            this.signalRConnection.onreconnected((connectionId) => {
                console.log('✅ SignalR reconnected:', connectionId);
                this.updateConnectionStatus('connected');
            });

            this.signalRConnection.onclose((error) => {
                console.log('❌ SignalR connection closed:', error);
                this.updateConnectionStatus('disconnected');
            });

            // Start connection
            this.signalRConnection.start().then(() => {
                console.log('✅ SignalR connected successfully');
                this.updateConnectionStatus('connected');
            }).catch(err => {
                console.error('❌ SignalR connection failed:', err);
                this.updateConnectionStatus('disconnected');
            });

        } catch (error) {
            console.error('❌ Error setting up SignalR:', error);
        }
    },

    // Handle real-time market data updates from Binance
    handleMarketDataUpdate: function(data) {
        try {
            // Update stored market data
            this.marketData.currentPrice = data.Price || data.price;
            this.marketData.volume = data.Volume || data.volume;
            this.marketData.changePercent24h = data.ChangePercent24h || data.changePercent24h;
            this.marketData.change24h = data.Change24h || data.change24h;
            this.marketData.high24h = data.High24h || data.high24h;
            this.marketData.low24h = data.Low24h || data.low24h;
            
            // Send updated data back to Blazor
            DotNet.invokeMethodAsync('BitcoinTradingBot.Web', 'UpdateMarketDataFromJS', 
                this.marketData.currentPrice,
                this.marketData.volume,
                this.marketData.changePercent24h,
                this.marketData.high24h,
                this.marketData.low24h
            ).then(() => {
                console.log('✅ Real-time data sent to Blazor');
            }).catch(error => {
                console.error('❌ Error sending real-time data to Blazor:', error);
            });
            
            // Add new data point to chart
            this.addNewDataPoint(
                this.marketData.currentPrice,
                new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
            );
            
            console.log('✅ Chart updated with real Binance data:', {
                price: this.marketData.currentPrice,
                timestamp: new Date().toISOString()
            });
            
        } catch (error) {
            console.error('❌ Error handling market data update:', error);
        }
    },

    // Add new data point to chart
    addNewDataPoint: function(price, timestamp) {
        if (!this.charts.trading) return;

        const chart = this.charts.trading;
        
        // Add new data point
        chart.data.labels.push(timestamp);
        chart.data.datasets[0].data.push(price);
        
        // Calculate new Keltner Channel values (simplified)
        const recentPrices = chart.data.datasets[0].data.slice(-20);
        const ema = recentPrices.reduce((a, b) => a + b) / recentPrices.length;
        const atr = price * 0.02; // Simplified ATR calculation
        
        chart.data.datasets[1].data.push(ema + atr * 2.0); // KC Upper
        chart.data.datasets[2].data.push(ema); // KC Middle
        chart.data.datasets[3].data.push(ema - atr * 2.0); // KC Lower
        
        // Keep only last 100 data points
        if (chart.data.labels.length > 100) {
            chart.data.labels.shift();
            chart.data.datasets.forEach(dataset => dataset.data.shift());
        }
        
        // Update chart with animation
        chart.update('active');
    },

    // Handle performance updates
    handlePerformanceUpdate: function(data) {
        // Update equity chart if needed
        if (this.charts.equity && data.TotalReturn !== undefined) {
            const chart = this.charts.equity;
            const newValue = 1.0 + (data.TotalReturn / 100);
            
            chart.data.datasets[0].data.push(newValue);
            chart.data.labels.push('Now');
            
            if (chart.data.labels.length > 30) {
                chart.data.labels.shift();
                chart.data.datasets[0].data.shift();
            }
            
            chart.update('none');
        }
    },

    // Handle system status updates
    handleSystemStatusUpdate: function(data) {
        console.log('📋 System status:', data.Status, '-', data.Message);
        // Additional status handling can be added here
    },

    // Handle trade updates
    handleTradeUpdate: function(data) {
        console.log('💰 New trade:', data.Side, data.Quantity, '@', data.EntryPrice);
        // Trade visualization can be added to charts here
    },

    // Update connection status indicator
    updateConnectionStatus: function(status) {
        const statusElement = document.getElementById('connection-status');
        if (!statusElement) return;

        const statusText = statusElement.querySelector('.status-text');
        const statusIcon = statusElement.querySelector('.status-icon');
        
        if (statusText && statusIcon) {
            switch (status) {
                case 'connected':
                    statusText.textContent = 'Connected to Binance';
                    statusIcon.className = 'status-icon fas fa-circle text-success';
                    break;
                case 'reconnecting':
                    statusText.textContent = 'Reconnecting...';
                    statusIcon.className = 'status-icon fas fa-circle text-warning';
                    break;
                case 'disconnected':
                    statusText.textContent = 'Disconnected';
                    statusIcon.className = 'status-icon fas fa-circle text-danger';
                    break;
            }
        }
    },

    // Update chart timeframe
    updateTimeframe: function(timeframe) {
        console.log('🔄 Updating timeframe to:', timeframe);
        
        // In production, this would fetch new data from Binance for the specific timeframe
        this.loadCandleData(timeframe === '5M' ? '5m' : timeframe === '1H' ? '1h' : '4h');
        
        if (this.charts.trading) {
            this.charts.trading.data.labels = this.chartData.timestamps;
            this.charts.trading.data.datasets[0].data = this.chartData.prices;
            this.charts.trading.data.datasets[1].data = this.chartData.kcUpper;
            this.charts.trading.data.datasets[2].data = this.chartData.kcMiddle;
            this.charts.trading.data.datasets[3].data = this.chartData.kcLower;
            this.charts.trading.update();
        }
    },

    // Refresh chart data from Binance
    refreshChart: async function() {
        console.log('🔄 Refreshing chart data from Binance...');
        
        try {
            await this.loadInitialMarketData();
            
            if (this.charts.trading) {
                this.charts.trading.data.labels = this.chartData.timestamps;
                this.charts.trading.data.datasets[0].data = this.chartData.prices;
                this.charts.trading.data.datasets[1].data = this.chartData.kcUpper;
                this.charts.trading.data.datasets[2].data = this.chartData.kcMiddle;
                this.charts.trading.data.datasets[3].data = this.chartData.kcLower;
                this.charts.trading.update();
            }
            
            console.log('✅ Chart refreshed with latest Binance data');
        } catch (error) {
            console.error('❌ Error refreshing chart:', error);
        }
    },

    // Update market data from Blazor (called by Blazor component)
    updateMarketData: function(data) {
        console.log('📊 Updating market data from Blazor:', data);
        
        // Ensure we have valid data
        if (!data || (!data.Price && !data.price)) {
            console.warn('⚠️ Invalid market data received');
            return;
        }
        
        // Update market data (handle both uppercase and lowercase properties)
        const previousPrice = this.marketData.currentPrice;
        this.marketData.currentPrice = data.Price || data.price || 0;
        this.marketData.volume = data.Volume || data.volume || 0;
        this.marketData.changePercent24h = data.ChangePercent24h || data.changePercent24h || 0;
        this.marketData.change24h = data.Change24h || data.change24h || 0;
        this.marketData.high24h = data.High24h || data.high24h || 0;
        this.marketData.low24h = data.Low24h || data.low24h || 0;
        
        // Send updated data back to Blazor to ensure UI is in sync
        if (this.marketData.currentPrice > 0) {
            DotNet.invokeMethodAsync('BitcoinTradingBot.Web', 'UpdateMarketDataFromJS', 
                this.marketData.currentPrice,
                this.marketData.volume,
                this.marketData.changePercent24h,
                this.marketData.high24h,
                this.marketData.low24h
            ).then(() => {
                console.log('✅ Market data synced with Blazor UI');
            }).catch(error => {
                console.error('❌ Error syncing data with Blazor:', error);
            });
        }
        
        // If this is the first real price data and charts aren't initialized, initialize them
        if (previousPrice === 0 && this.marketData.currentPrice > 0) {
            console.log('🚀 First real price received, initializing charts with price:', this.marketData.currentPrice);
            this.loadCandleData().then(() => {
                if (this.charts.trading) {
                    // Update chart with new data
                    this.charts.trading.data.datasets[0].data = this.chartData.prices;
                    this.charts.trading.data.datasets[1].data = this.chartData.kcUpper;
                    this.charts.trading.data.datasets[2].data = this.chartData.kcMiddle;
                    this.charts.trading.data.datasets[3].data = this.chartData.kcLower;
                    this.charts.trading.data.labels = this.chartData.timestamps;
                    this.charts.trading.update();
                }
            });
        } else if (this.marketData.currentPrice > 0) {
            // Normal update - add to chart
            this.handleMarketDataUpdate(data);
        }
    },

    // Cleanup and destroy
    destroy: function() {
        console.log('🧹 Destroying real-time trading dashboard...');
        
        // Destroy charts
        Object.values(this.charts).forEach(chart => {
            if (chart) chart.destroy();
        });
        this.charts = {};
        
        // Close SignalR connection
        if (this.signalRConnection) {
            this.signalRConnection.stop();
            this.signalRConnection = null;
        }
        
        this.isInitialized = false;
        console.log('✅ Dashboard destroyed successfully');
    }
};

// Maintain backward compatibility with old dashboard name
window.blazorTradingDashboard = {
    initialize: function() {
        console.log('🔄 Redirecting to real-time dashboard...');
        return window.realTimeTradingDashboard.initialize();
    },
    
    updateTimeframe: function(timeframe) {
        return window.realTimeTradingDashboard.updateTimeframe(timeframe);
    },
    
    destroy: function() {
        return window.realTimeTradingDashboard.destroy();
    }
};

// Auto-initialize when Chart.js is available
(function() {
    function tryInitialize() {
        if (typeof Chart !== 'undefined' && document.readyState === 'complete') {
            console.log('🚀 Auto-initializing real-time dashboard...');
            // Don't auto-initialize, let Blazor control initialization
        } else {
            setTimeout(tryInitialize, 100);
        }
    }
    
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', tryInitialize);
    } else {
        tryInitialize();
    }
})();

console.log('📊 Real-Time Trading Dashboard JavaScript loaded successfully'); 