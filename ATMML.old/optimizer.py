import cvxpy as cp
import numpy as np

def compute_volatility_weighted_share_sizes(
    portfolio_value,
    tickers,
    positions,
    prices,
    price_history=None,
    target_volatility=None
):
    """
    Compute optimal share sizes using volatility-aware portfolio sizing.

    Parameters:
        portfolio_value: float
        tickers: list of str
        positions: list of int (1 = long, -1 = short, 0 = neutral)
        prices: list of float
        price_history: 2D list or np.ndarray [time x tickers] (optional)
        target_volatility: float, e.g., 0.05 (optional, annualized)
    Returns:
        dict with keys: status, tickers, share_sizes
    """
    n = len(tickers)
    prices = np.array(prices)
    positions = np.array(positions)

    x = cp.Variable(n)  # number of shares

    constraints = []

    # Constraint 1: position size ? 10% of portfolio value
    constraints.append(cp.abs(cp.multiply(x, prices)) <= 0.10 * portfolio_value)

    # Constraint 2: net exposure ? 10% of portfolio value
    net_exposure = cp.sum(cp.multiply(x * prices, positions))
    constraints.append(cp.abs(net_exposure) <= 0.10 * portfolio_value)

    if price_history is None:
        # Fallback to constraint-only sizing
        objective = cp.Minimize(cp.sum(cp.abs(cp.multiply(x, prices))))
    else:
        # Volatility-based sizing using covariance
        price_history = np.array(price_history)
        returns = price_history[1:] / price_history[:-1] - 1  # daily returns
        cov = np.cov(returns.T)  # covariance matrix
        weights = cp.multiply(x, prices) / portfolio_value
        portfolio_variance = cp.quad_form(weights, cov)

        if target_volatility is not None:
            constraints.append(portfolio_variance <= target_volatility**2)
            objective = cp.Minimize(cp.sum(cp.abs(cp.multiply(x, prices))))
        else:
            objective = cp.Minimize(portfolio_variance)

    problem = cp.Problem(objective, constraints)
    problem.solve()

    return {
        "status": problem.status,
        "tickers": tickers,
        "share_sizes": x.value.tolist() if x.value is not None else None
    }
