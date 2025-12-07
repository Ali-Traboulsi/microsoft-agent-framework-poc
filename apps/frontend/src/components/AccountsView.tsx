import React, { useEffect } from 'react';
import { accountsApi } from '../services/api';
import { useStore } from '../store/store';

export const AccountsView: React.FC = () => {
  const { accounts, setAccounts, selectedAccount, setSelectedAccount } = useStore();

  useEffect(() => {
    loadAccounts();
  }, []);

  const loadAccounts = async () => {
    try {
      const response = await accountsApi.getAll();
      if (response.data.success && response.data.data) {
        setAccounts(response.data.data);
      }
    } catch (error) {
      console.error('Failed to load accounts:', error);
    }
  };

  const getStatusColor = (status: number) => {
    // AccountStatus enum: 0=Active, 1=Suspended, 2=Closed
    switch (status) {
      case 0: // Active
        return 'bg-green-100 text-green-700';
      case 1: // Suspended
        return 'bg-yellow-100 text-yellow-700';
      case 2: // Closed
        return 'bg-red-100 text-red-700';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  const getStatusLabel = (status: number) => {
    const labels = ['Active', 'Suspended', 'Closed'];
    return labels[status] || 'Unknown';
  };

  return (
    <div className="p-6">
      <div className="mb-6">
        <h2 className="text-2xl font-bold mb-2">Accounts</h2>
        <p className="text-gray-600">View customer account information</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {accounts.map((account) => (
          <div
            key={account.accountId}
            onClick={() => setSelectedAccount(account)}
            className={`p-6 bg-white rounded-lg shadow cursor-pointer hover:shadow-lg transition-shadow border-2 ${
              selectedAccount?.accountId === account.accountId
                ? 'border-blue-500'
                : 'border-transparent'
            }`}
          >
            <div className="flex justify-between items-start mb-4">
              <div>
                <h3 className="font-bold text-lg">{account.customerName}</h3>
                <p className="text-sm text-gray-500">{account.accountId}</p>
              </div>
              <span className={`px-2 py-1 rounded text-xs font-medium ${getStatusColor(account.status)}`}>
                {getStatusLabel(account.status)}
              </span>
            </div>

            <div className="mb-4">
              <p className="text-2xl font-bold text-blue-600">
                ${account.balance.toLocaleString()}
              </p>
              <p className="text-xs text-gray-500">{account.currency}</p>
            </div>

            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">Customer ID:</span>
                <span className="font-medium">{account.customerId}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">Created:</span>
                <span className="font-medium">
                  {new Date(account.createdDate).toLocaleDateString()}
                </span>
              </div>
            </div>
          </div>
        ))}

        {accounts.length === 0 && (
          <div className="col-span-full text-center py-12 text-gray-500">
            <p className="text-lg">No accounts found</p>
          </div>
        )}
      </div>

      {selectedAccount && (
        <div className="mt-6 p-6 bg-white rounded-lg shadow">
          <h3 className="text-xl font-bold mb-4">Account Details</h3>
          <div className="grid md:grid-cols-2 gap-6">
            <div>
              <h4 className="font-medium mb-2">Account Information</h4>
              <dl className="space-y-2">
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Account ID:</dt>
                  <dd className="font-medium">{selectedAccount.accountId}</dd>
                </div>
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Customer ID:</dt>
                  <dd className="font-medium">{selectedAccount.customerId}</dd>
                </div>
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Customer Name:</dt>
                  <dd className="font-medium">{selectedAccount.customerName}</dd>
                </div>
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Status:</dt>
                  <dd className={`font-medium ${getStatusColor(selectedAccount.status)}`}>
                    {getStatusLabel(selectedAccount.status)}
                  </dd>
                </div>
                <div className="flex justify-between text-sm">
                  <dt className="text-gray-600">Created:</dt>
                  <dd className="font-medium">
                    {new Date(selectedAccount.createdDate).toLocaleDateString()}
                  </dd>
                </div>
              </dl>
            </div>

            <div>
              <h4 className="font-medium mb-2">Balance</h4>
              <div className="p-4 bg-blue-50 rounded-lg">
                <p className="text-3xl font-bold text-blue-600">
                  ${selectedAccount.balance.toLocaleString()}
                </p>
                <p className="text-sm text-gray-600 mt-1">{selectedAccount.currency}</p>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
