function [stress, shifted, detached] = spindle_trial_stress(hs, deltaLengthNm)
% Pure trial geometry using authoritative populations; no cached distribution.
% This is a force-balance building block, not a complete slack/body controller.
  if ~isscalar(deltaLengthNm) || ~isfinite(deltaLengthNm)
    error('DNNE:SpindleTrialLength', 'Trial displacement must be finite and scalar.');
  end
  bins = reshape(hs.y(2:end-2), 1, []);
  shifted = interp1(hs.x_bins, bins, hs.x_bins - deltaLengthNm * hs.compliance_factor, 'linear', 0);
  detached = hs.y(1) + sum(bins) - sum(shifted);
  crossBridge = hs.cb_number_density * hs.k_cb * 1e-9 * sum((hs.x_bins + hs.power_stroke) .* shifted);
  passive = hs.k_passive * (hs.hs_length + deltaLengthNm - hs.hsl_slack);
  stress = crossBridge + passive;
end
