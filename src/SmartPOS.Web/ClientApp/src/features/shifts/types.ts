export interface ShiftListItem {
  id: string;
  shiftNumber: string;
  registerName: string;
  storeName: string;
  openedByName: string;
  closedByName: string | null;
  status: string;
  openingFloat: number;
  expectedCash: number;
  countedCash: number;
  variance: number;
  openedOn: string;
  closedOn: string | null;
}

export interface RegisterOption {
  id: string;
  storeId: string;
  display: string;
  hasCashDrawer: boolean;
  hasOpenShift: boolean;
}

export interface ShiftReport {
  shiftId: string;
  shiftNumber: string;
  registerName: string;
  storeName: string;
  openedByName: string;
  status: string;
  openedOn: string;
  closedOn: string | null;
  openingFloat: number;
  cashSales: number;
  cashRefunds: number;
  cashIn: number;
  cashOut: number;
  noSaleDrawerOpenCount: number;
  expectedCash: number;
  countedCash: number;
  variance: number;
  saleCount: number;
  cardSales: number;
  creditSales: number;
  grossSales: number;
  denominations: Array<{
    denominationValue: number;
    quantity: number;
    amount: number;
    sortOrder: number;
  }>;
}

export interface CashDenominationLine {
  denominationValue: number;
  quantity: number;
  sortOrder: number;
}
