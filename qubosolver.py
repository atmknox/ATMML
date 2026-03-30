# qubosolver.py
import dimod
from neal import SimulatedAnnealingSampler
from dwave.system import LeapHybridSampler

# Example mapping: stock index -> (ticker, shares per unit)
STOCK_MAP = {
    0: ("AAPL", 1000),   # 1000 shares per unit
    1: ("MSFT", 500),    # 500 shares per unit
}

def solve_local(Q):
    sampler = SimulatedAnnealingSampler()
    sampleset = sampler.sample_qubo(Q, num_reads=100)
    best = sampleset.first.sample
    return convert_to_shares(best, "local")

def solve_leap(Q):
    sampler = LeapHybridSampler()
    sampleset = sampler.sample_qubo(Q, time_limit=5)
    best = sampleset.first.sample
    return convert_to_shares(best, "leap")

def convert_to_shares(sample, solver_name):
    """Convert binary solution -> shares."""
    allocations = {}
    for idx, bit in sample.items():
        ticker, per_unit = STOCK_MAP.get(idx, (f"Var{idx}", 100))
        allocations[ticker] = allocations.get(ticker, 0) + bit * per_unit
    return {
        "allocations": allocations,
        "solver": solver_name
    }
