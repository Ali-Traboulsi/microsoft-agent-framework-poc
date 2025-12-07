// Format currency
export const formatCurrency = (value: number, currency: string = 'SAR') => {
  return new Intl.NumberFormat('en-SA', {
    style: 'currency',
    currency: currency,
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
};

// Format percentage
export const formatPercent = (value: number) => `${value.toFixed(1)}%`;

