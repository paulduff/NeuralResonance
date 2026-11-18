"""Restore the arithmetic layout of saved NumPy eigenvector witnesses.

NumPy 2.2.6's real eig result is a real view of complex storage. NPZ preserves
values and dtype, but packs away that stride. BLAS can then sum in a different
order. This restores the declared layout; it does not change any error bound.
"""
import numpy as np

CANONICAL_LAYOUT='contiguous-c-v1'
LEGACY_LAYOUT='numpy-2.2.6-eig-real-view-v1'


def arithmetic_layout(values,vectors,layout):
    assert layout in (CANONICAL_LAYOUT,LEGACY_LAYOUT),'Unknown eigen-witness layout.'
    assert values.dtype==vectors.dtype and values.dtype in (np.dtype('float64'),np.dtype('complex128'))
    if layout==LEGACY_LAYOUT and values.dtype==np.float64:
        # Reproduce the actual legacy eig layout, not a numerically close result.
        w=np.empty(values.shape,dtype=np.complex128).real;w[:]=values
        v=np.empty(vectors.shape,dtype=np.complex128).real;v[:]=vectors
        return w,v
    return np.array(values,copy=True,order='C'),np.array(vectors,copy=True,order='C')
