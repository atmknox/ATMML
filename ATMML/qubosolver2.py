# qubosolver2.py
# Robust helper with dependency fallbacks + debug hooks

# --- Optional solvers (import only if available) ---
_have_neal = False
_have_dimod = False
try:
    from neal import SimulatedAnnealingSampler  # local simulator
    _have_neal = True
except Exception:
    pass

try:
    import dimod  # for exact solver or SampleSet if available
    _have_dimod = True
except Exception:
    pass

# --- Minimal no-deps brute-force for small problems (debug) ---
def _bruteforce_qubo(Q):
    # Q is dict with keys (i,i) or (i,j) and float values
    # Build variable index set
    vars_set = set()
    for (i, j) in Q.keys():
        vars_set.add(i); vars_set.add(j)
    vars_list = sorted(vars_set)
    n = len(vars_list)
    if n > 20:
        raise ValueError("Bruteforce fallback is for small n only (<=20).")

    # Compute energy for each assignment
    best_sample = None
    best_energy = None
    from itertools import product
    for bits in product([0,1], repeat=n):
        x = {vars_list[k]: bits[k] for k in range(n)}
        e = 0.0
        for (i,j), w in Q.items():
            if i == j:
                e += w * x[i]
            else:
                e += w * x[i] * x[j]
        if (best_energy is None) or (e < best_energy):
            best_energy = e
            best_sample = x
    class _SS:
        def __init__(self, sample, energy):
            self.first = type("First", (), {"sample": sample, "energy": energy})()
    return _SS(best_sample, best_energy)

# Example: var index -> (ticker, shares per unit)
STOCK_MAP = {
    0: ("AAPL", 1000),
    1: ("MSFT", 500),
}

def _convert_to_shares(sample, solver_name):
    allocations = {}
    for idx, bit in sample.items():
        ticker, per_unit = STOCK_MAP.get(idx, (f"Var{idx}", 100))
        allocations[ticker] = allocations.get(ticker, 0) + int(bit) * int(per_unit)
    return {"allocations": allocations, "solver": solver_name}

# Public: quick sanity hook from C#
def ping(arg):
    """Return basic info about the single argument you passed (type + length)."""
    t = str(type(arg))
    try:
        keys = list(arg.keys())
    except Exception:
        keys = None
    return {"arg_type": t, "keys": keys}

def solve_local(Q):
    """
    Solve a QUBO locally. Tries neal -> dimod.ExactSolver -> bruteforce fallback.
    Q must be a Python dict with keys as (i,j) tuples and float values.
    """
    # neal
    if _have_neal:
        sampler = SimulatedAnnealingSampler()
        ss = sampler.sample_qubo(Q, num_reads=100)
        best = ss.first.sample
        return _convert_to_shares(best, "local-neal")

    # dimod exact
    if _have_dimod:
        exact = dimod.ExactSolver()
        ss = exact.sample_qubo(Q)
        best = ss.first.sample
        return _convert_to_shares(best, "local-dimod-exact")

    # pure python brute force (small problems)
    ss = _bruteforce_qubo(Q)
    return _convert_to_shares(ss.first.sample, "local-bruteforce")
