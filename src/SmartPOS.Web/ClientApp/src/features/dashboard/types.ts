export interface DashboardTrendPoint {
  label: string;
  count: number;
  amount: number;
  profit: number;
}
export interface DashboardPaymentMix {
  method: string;
  amount: number;
  count: number;
  sharePercent: number;
}
export interface DashboardStorePerformance {
  storeId: string;
  storeName: string;
  netSales: number;
  grossProfit: number;
  marginPercent: number;
  transactions: number;
  inventoryValue: number;
  lowStockItems: number;
}
export interface DashboardSummary {
  productCount: number;
  activeProductCount: number;
  lowStockCount: number;
  criticalStockCount: number;
  inventoryRetailValue: number;
  movementsToday: number;
  storeCount: number;
  registerCount: number;
  salesLive: boolean;
  todaySales: number;
  todayTransactions: number;
  todayProfit: number;
  todayMarginPercent: number;
  todayDiscounts: number;
  averageBasket: number;
  activityTrend: DashboardTrendPoint[];
  storePerformance: DashboardStorePerformance[];
  paymentMix: DashboardPaymentMix[];
}
export interface AlertSummary {
  total: number;
  critical: number;
  warning: number;
  info: number;
}
export interface SmartAlert {
  alertKey: string;
  severity: string;
  type: string;
  title: string;
  message: string;
  storeName: string;
  actionText?: string | null;
  actionUrl?: string | null;
}
export interface DashboardData {
  summary: DashboardSummary;
  alertSummary: AlertSummary;
  topAlerts: SmartAlert[];
}
