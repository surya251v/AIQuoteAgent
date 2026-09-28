import { useEffect, useState } from "react";
import "./App.css";

const API_URL = "http://localhost:5233";

type Service = {
  id: number;
  name: string;
  unit: string;
  price: number;
};

type QuoteItem = {
  serviceId: number;
  serviceName: string;
  unit: string;
  quantity: number;
  unitPrice: number;
  total: number;
};

type Quote = {
  quoteId: number;
  customerId: number;
  customerName: string;
  customerPhone: string;
  customerMessage: string;
  createdAt: string;
  status: string;
  items: QuoteItem[];
  grandTotal: number;
};

type QuoteHistoryItem = {
  quoteId: number;
  customerName: string;
  customerPhone: string;
  createdAt: string;
  status: string;
  totalAmount: number;
  itemCount: number;
};

function App() {
  const [customerName, setCustomerName] = useState("");
  const [customerPhone, setCustomerPhone] = useState("");
  const [customerAddress, setCustomerAddress] = useState("");
  const [message, setMessage] = useState("");

  const [quote, setQuote] = useState<Quote | null>(null);
  const [quotes, setQuotes] = useState<QuoteHistoryItem[]>([]);

  const [services, setServices] = useState<Service[]>([]);

  const [serviceName, setServiceName] = useState("");
  const [serviceUnit, setServiceUnit] = useState("");
  const [servicePrice, setServicePrice] = useState("");

  const [editingServiceId, setEditingServiceId] =
    useState<number | null>(null);

  const [loading, setLoading] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [loadingServices, setLoadingServices] = useState(false);

  const [error, setError] = useState("");
  const [serviceError, setServiceError] = useState("");

  useEffect(() => {
    loadQuoteHistory();
    loadServices();
  }, []);

  // -----------------------------
  // SERVICES
  // -----------------------------

  const loadServices = async () => {
    try {
      setLoadingServices(true);

      const response = await fetch(
        `${API_URL}/api/services`
      );

      if (!response.ok) {
        throw new Error("Could not load services.");
      }

      const data = await response.json();

      setServices(data);
    } catch (err) {
      setServiceError(
        err instanceof Error
          ? err.message
          : "Could not load services."
      );
    } finally {
      setLoadingServices(false);
    }
  };

  const clearServiceForm = () => {
    setServiceName("");
    setServiceUnit("");
    setServicePrice("");
    setEditingServiceId(null);
    setServiceError("");
  };

  const saveService = async () => {
    setServiceError("");

    if (!serviceName.trim()) {
      setServiceError("Please enter service name.");
      return;
    }

    if (!serviceUnit.trim()) {
      setServiceError("Please enter unit.");
      return;
    }

    const price = Number(servicePrice);

    if (!servicePrice || Number.isNaN(price) || price < 0) {
      setServiceError("Please enter a valid price.");
      return;
    }

    try {
      const serviceData = {
        name: serviceName,
        unit: serviceUnit,
        price: price,
      };

      let response;

      if (editingServiceId === null) {
        // CREATE
        response = await fetch(
          `${API_URL}/api/services`,
          {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
            },
            body: JSON.stringify(serviceData),
          }
        );
      } else {
        // UPDATE
        response = await fetch(
          `${API_URL}/api/services/${editingServiceId}`,
          {
            method: "PUT",
            headers: {
              "Content-Type": "application/json",
            },
            body: JSON.stringify({
              id: editingServiceId,
              ...serviceData,
            }),
          }
        );
      }

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(
          errorText || "Could not save service."
        );
      }

      clearServiceForm();

      await loadServices();
    } catch (err) {
      setServiceError(
        err instanceof Error
          ? err.message
          : "Could not save service."
      );
    }
  };

  const editService = (service: Service) => {
    setEditingServiceId(service.id);
    setServiceName(service.name);
    setServiceUnit(service.unit);
    setServicePrice(service.price.toString());

    window.scrollTo({
      top: 0,
      behavior: "smooth",
    });
  };

  const deleteService = async (id: number) => {
    const confirmed = window.confirm(
      "Are you sure you want to delete this service?"
    );

    if (!confirmed) return;

    try {
      const response = await fetch(
        `${API_URL}/api/services/${id}`,
        {
          method: "DELETE",
        }
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(
          errorText || "Could not delete service."
        );
      }

      await loadServices();
    } catch (err) {
      setServiceError(
        err instanceof Error
          ? err.message
          : "Could not delete service."
      );
    }
  };

  // -----------------------------
  // QUOTE HISTORY
  // -----------------------------

  const loadQuoteHistory = async () => {
    try {
      setLoadingHistory(true);

      const response = await fetch(
        `${API_URL}/api/quotes`
      );

      if (!response.ok) {
        throw new Error(
          "Could not load quote history."
        );
      }

      const data = await response.json();

      setQuotes(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoadingHistory(false);
    }
  };

  // -----------------------------
  // CREATE QUOTE
  // -----------------------------

  const generateQuote = async () => {
    setError("");
    setQuote(null);

    if (!customerName.trim()) {
      setError("Please enter customer name.");
      return;
    }

    if (!customerPhone.trim()) {
      setError("Please enter customer phone.");
      return;
    }

    if (!message.trim()) {
      setError(
        "Please enter customer requirement."
      );
      return;
    }

    try {
      setLoading(true);

      const customerResponse = await fetch(
        `${API_URL}/api/customers/upsert`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            name: customerName,
            phone: customerPhone,
            address: customerAddress,
          }),
        }
      );

      if (!customerResponse.ok) {
        const errorText =
          await customerResponse.text();

        throw new Error(
          `Customer creation failed: ${errorText}`
        );
      }

      const customer =
        await customerResponse.json();

      const quoteResponse = await fetch(
        `${API_URL}/api/quotes/ai`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            customerId: customer.id,
            message: message,
          }),
        }
      );

      if (!quoteResponse.ok) {
        const errorText =
          await quoteResponse.text();

        throw new Error(
          `Quote generation failed: ${errorText}`
        );
      }

      const quoteData =
        await quoteResponse.json();

      setQuote(quoteData);

      await loadQuoteHistory();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Something went wrong."
      );
    } finally {
      setLoading(false);
    }
  };

  const downloadPdf = (quoteId: number) => {
    window.open(
      `${API_URL}/api/quotes/${quoteId}/pdf`,
      "_blank"
    );
  };

  const formatDate = (date: string) => {
    return new Date(date).toLocaleString("en-IN");
  };

  return (
    <div className="app">

      {/* HEADER */}

      <header className="header">
        <h1>AI Quote Agent</h1>

        <p>
          Create professional quotations using AI
        </p>
      </header>

      <main className="container">

        {/* PRICE LIST */}

        <section className="card">

          <div className="section-title">

            <div>
              <h2>Price List</h2>

              <p>
                Manage your services and prices
              </p>
            </div>

          </div>

          <div className="service-form">

            <div className="form-group">
              <label>Service Name</label>

              <input
                type="text"
                placeholder="Example: AC Installation"
                value={serviceName}
                onChange={(e) =>
                  setServiceName(e.target.value)
                }
              />
            </div>

            <div className="form-group">
              <label>Unit</label>

              <input
                type="text"
                placeholder="Example: AC"
                value={serviceUnit}
                onChange={(e) =>
                  setServiceUnit(e.target.value)
                }
              />
            </div>

            <div className="form-group">
              <label>Price</label>

              <input
                type="number"
                placeholder="Example: 1200"
                value={servicePrice}
                onChange={(e) =>
                  setServicePrice(e.target.value)
                }
              />
            </div>

          </div>

          <div className="service-actions">

            <button
              className="generate-button"
              onClick={saveService}
            >
              {editingServiceId === null
                ? "Add Service"
                : "Update Service"}
            </button>

            {editingServiceId !== null && (
              <button
                className="cancel-button"
                onClick={clearServiceForm}
              >
                Cancel
              </button>
            )}

          </div>

          {serviceError && (
            <div className="error">
              {serviceError}
            </div>
          )}

          <div className="service-table-wrapper">

            {loadingServices ? (

              <p>Loading services...</p>

            ) : services.length === 0 ? (

              <p>No services configured.</p>

            ) : (

              <table className="quote-table">

                <thead>
                  <tr>
                    <th>Service</th>
                    <th>Unit</th>
                    <th>Price</th>
                    <th>Actions</th>
                  </tr>
                </thead>

                <tbody>

                  {services.map((service) => (

                    <tr key={service.id}>

                      <td>
                        {service.name}
                      </td>

                      <td>
                        {service.unit}
                      </td>

                      <td>
                        ₹
                        {service.price.toLocaleString(
                          "en-IN"
                        )}
                      </td>

                      <td>

                        <button
                          className="small-button"
                          onClick={() =>
                            editService(service)
                          }
                        >
                          Edit
                        </button>

                        <button
                          className="delete-button"
                          onClick={() =>
                            deleteService(service.id)
                          }
                        >
                          Delete
                        </button>

                      </td>

                    </tr>

                  ))}

                </tbody>

              </table>

            )}

          </div>

        </section>


        {/* CREATE QUOTE */}

        <section className="card">

          <h2>Create Quote</h2>

          <div className="form-grid">

            <div className="form-group">

              <label>
                Customer Name
              </label>

              <input
                type="text"
                placeholder="Enter customer name"
                value={customerName}
                onChange={(e) =>
                  setCustomerName(e.target.value)
                }
              />

            </div>

            <div className="form-group">

              <label>Phone</label>

              <input
                type="text"
                placeholder="Enter phone number"
                value={customerPhone}
                onChange={(e) =>
                  setCustomerPhone(e.target.value)
                }
              />

            </div>

          </div>

          <div className="form-group">

            <label>Address</label>

            <input
              type="text"
              placeholder="Enter customer address"
              value={customerAddress}
              onChange={(e) =>
                setCustomerAddress(e.target.value)
              }
            />

          </div>

          <div className="form-group">

            <label>
              Customer Requirement
            </label>

            <textarea
              rows={5}
              placeholder="Example: I need AC installation for 2 ACs and 10 meters of copper pipe."
              value={message}
              onChange={(e) =>
                setMessage(e.target.value)
              }
            />

          </div>

          <button
            className="generate-button"
            onClick={generateQuote}
            disabled={loading}
          >
            {loading
              ? "Generating Quote..."
              : "Generate Quote"}
          </button>

          {error && (
            <div className="error">
              {error}
            </div>
          )}

        </section>


        {/* CURRENT QUOTE */}

        {quote && (

          <section className="card">

            <div className="quote-header">

              <div>

                <h2>
                  Quotation #{quote.quoteId}
                </h2>

                <p>
                  Customer: {quote.customerName}
                </p>

                <p>
                  Phone: {quote.customerPhone}
                </p>

              </div>

              <button
                className="pdf-button"
                onClick={() =>
                  downloadPdf(quote.quoteId)
                }
              >
                Download PDF
              </button>

            </div>

            <table className="quote-table">

              <thead>

                <tr>
                  <th>Service</th>
                  <th>Qty</th>
                  <th>Unit</th>
                  <th>Price</th>
                  <th>Total</th>
                </tr>

              </thead>

              <tbody>

                {quote.items.map((item) => (

                  <tr key={item.serviceId}>

                    <td>
                      {item.serviceName}
                    </td>

                    <td>
                      {item.quantity}
                    </td>

                    <td>
                      {item.unit}
                    </td>

                    <td>
                      ₹
                      {item.unitPrice.toLocaleString(
                        "en-IN"
                      )}
                    </td>

                    <td>
                      ₹
                      {item.total.toLocaleString(
                        "en-IN"
                      )}
                    </td>

                  </tr>

                ))}

              </tbody>

            </table>

            <div className="grand-total">

              Total: ₹
              {quote.grandTotal.toLocaleString(
                "en-IN"
              )}

            </div>

          </section>

        )}


        {/* QUOTE HISTORY */}

        <section className="card">

          <div className="history-header">

            <div>

              <h2>Quote History</h2>

              <p>
                Previously generated quotations
              </p>

            </div>

            <button
              className="refresh-button"
              onClick={loadQuoteHistory}
            >
              Refresh
            </button>

          </div>

          {loadingHistory ? (

            <p>Loading quote history...</p>

          ) : quotes.length === 0 ? (

            <p>No quotations found.</p>

          ) : (

            <div className="history-table-wrapper">

              <table className="quote-table">

                <thead>

                  <tr>
                    <th>Quote #</th>
                    <th>Customer</th>
                    <th>Phone</th>
                    <th>Date</th>
                    <th>Items</th>
                    <th>Amount</th>
                    <th>Status</th>
                    <th>Action</th>
                  </tr>

                </thead>

                <tbody>

                  {quotes.map((item) => (

                    <tr key={item.quoteId}>

                      <td>
                        #{item.quoteId}
                      </td>

                      <td>
                        {item.customerName}
                      </td>

                      <td>
                        {item.customerPhone}
                      </td>

                      <td>
                        {formatDate(
                          item.createdAt
                        )}
                      </td>

                      <td>
                        {item.itemCount}
                      </td>

                      <td>
                        ₹
                        {item.totalAmount.toLocaleString(
                          "en-IN"
                        )}
                      </td>

                      <td>
                        {item.status}
                      </td>

                      <td>

                        <button
                          className="small-button"
                          onClick={() =>
                            downloadPdf(
                              item.quoteId
                            )
                          }
                        >
                          PDF
                        </button>

                      </td>

                    </tr>

                  ))}

                </tbody>

              </table>

            </div>

          )}

        </section>

      </main>

    </div>
  );
}

export default App;