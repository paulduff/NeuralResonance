function run_spindle_state_controls(package, rootsOnly)
% Mechanistic state/force-query controls, executed only in the visible window.
  if nargin < 2, rootsOnly = false; end
  if rootsOnly, stage = 'preflight'; else, stage = 'post-trajectory'; end
  fibers = {'bag', 'chain'}; records = cell(1, 2); roots = cell(1, 2);
  for f = 1:2
    [hs, parameters] = create_fiber(fibers{f});
    expectedDelta = hs.hsl_slack - hs.hs_length;
    probes = [expectedDelta-1, expectedDelta, expectedDelta+1, 0];
    stresses = zeros(size(probes)); expectedStresses = zeros(size(probes));
    for k = 1:numel(probes)
      stresses(k) = spindle_trial_stress(hs, probes(k));
      expectedStresses(k) = hs.k_passive * (hs.hs_length + probes(k) - hs.hsl_slack);
    end
    [delta, residual, info, output] = fzero(@(dx) spindle_trial_stress(hs, dx), 0, optimset('display', 'off'));
    bracketError = ''; bracketDelta = NaN; bracketResidual = NaN; bracketInfo = 0; bracketOutput = struct();
    try
      [bracketDelta, bracketResidual, bracketInfo, bracketOutput] = fzero(@(dx) spindle_trial_stress(hs, dx), ...
        [expectedDelta-1, expectedDelta+1], optimset('display', 'off'));
    catch failure
      bracketError = failure.message;
    end
    roots{f} = struct('fiber', fibers{f}, 'initialLengthNm', hs.hs_length, 'slackLengthNm', hs.hsl_slack, ...
      'initialState', hs.y, 'parameters', parameters, 'probeDisplacementsNm', probes, 'trialStressesPa', stresses, ...
      'analyticPassiveStressesPa', expectedStresses, 'scalarGuessDeltaNm', delta, ...
      'scalarGuessRootNm', delta + hs.hs_length, 'scalarGuessResidualPa', residual, 'scalarGuessInfo', info, ...
      'scalarGuessOutput', output, 'bracketDeltaNm', bracketDelta, 'bracketRootNm', bracketDelta + hs.hs_length, ...
      'bracketResidualPa', bracketResidual, 'bracketInfo', bracketInfo, 'bracketOutput', bracketOutput, 'bracketError', bracketError, ...
      'initialStatePreserved', isequal(hs.y, [1, zeros(1,81), 1, 0]));
  end
  rootDiagnostics = struct('version', 'unloaded-spindle-root-diagnostic-v1', 'stage', stage, 'fibers', {roots}, ...
    'boundChanged', false, 'kineticsExecuted', false);
  matPath = fullfile(package, [stage '-root-diagnostics.mat']);
  if exist(matPath, 'file'), error('Preserve previous root diagnostics.'); end
  save(matPath, 'rootDiagnostics', '-v7');
  write_json(fullfile(package, [stage '-root-diagnostics.json']), rootDiagnostics);
  for f = 1:2
    fprintf('%s unloaded %s root: scalar=%.17g nm, bracket=%.17g nm, expected=%.17g nm\n', ...
      stage, fibers{f}, roots{f}.scalarGuessRootNm, roots{f}.bracketRootNm, roots{f}.slackLengthNm);
  end
  assert(all(cellfun(@(r) abs(r.scalarGuessRootNm-r.slackLengthNm) <= 1e-9, roots)));
  if rootsOnly, return; end
  for f = 1:2
    name = fibers{f}; [hs, parameters] = create_fiber(name);
    state0 = hs.y;
    root = roots{f}.scalarGuessRootNm;
    assert(abs(root - hs.hsl_slack) <= 1e-9);
    props = {'bin_pops', 'no_detached', 'f_bound'}; readonly = false(1, 3);
    for k = 1:3
      try, hs.(props{k}) = 0; catch, readonly(k) = true; end
      assert(isequal(hs.y, state0));
    end
    assert(all(readonly));
    for k = 1:400, hs.forwardStep(0.00025, 0, 0, 1, 1); end
    hs.forwardStep(0, 3, 3, 0, 1);
    shiftedError = max(abs(hs.bin_pops(:) - reshape(hs.y(2:end-2), [], 1)));
    assert(shiftedError == 0);
    stored = hs.y; storedLength = hs.hs_length; storedOverlap = hs.f_overlap;
    queries = [-100, -3, 0, 3, 100]; forceError = 0; massError = 0;
    queryRows = cell(1, numel(queries));
    for k = 1:numel(queries)
      delta = queries(k);
      [predicted, bins, detached] = spindle_trial_stress(hs, delta);
      [copy, unused] = create_fiber(name);
      copy.y = stored; copy.hs_length = storedLength; copy.cmd_length = hs.cmd_length;
      copy.forwardStep(0, delta, delta, 0, 1);
      forceError = max(forceError, abs(predicted - copy.hs_force));
      massError = max(massError, abs(detached + sum(bins) - sum(stored(1:end-2))));
      assert(max(abs(bins(:) - reshape(copy.y(2:end-2), [], 1))) <= 1e-12);
      assert(abs(detached - copy.y(1)) <= 1e-12);
      queryRows{k} = struct('deltaLengthNm', delta, 'trialStressPa', predicted, 'appliedStressPa', copy.hs_force, ...
        'shiftedBins', bins, 'detached', detached);
    end
    pure = isequal(stored, hs.y) && storedLength == hs.hs_length && storedOverlap == hs.f_overlap;
    assert(pure && forceError <= 1e-7 && massError <= 1e-12);
    boundaryState = hs.y;
    hs.hs_length = hs.thick_filament_length + hs.thin_filament_length;
    hs.update_filamentOverlap(); assert(hs.f_overlap == 0);
    rejectedZero = false;
    try, hs.evolve_cbDist(.00025); catch failure, rejectedZero = strcmp(failure.identifier, 'DNNE:SpindleOverlapDomain'); end
    preserveZero = isequal(boundaryState, hs.y);
    hs.hs_length = 100; hs.update_filamentOverlap(); assert(hs.f_overlap < 0);
    rejectedInvalid = false;
    try, hs.evolve_cbDist(.00025); catch failure, rejectedInvalid = strcmp(failure.identifier, 'DNNE:SpindleOverlapDomain'); end
    preserved = preserveZero && isequal(boundaryState, hs.y);
    assert(rejectedZero && rejectedInvalid && preserved);
    records{f} = struct('fiber', name, 'parameters', parameters, 'readOnlyProperties', readonly, ...
      'initialRootNm', root, 'slackLengthNm', hs.hsl_slack, 'shiftedDerivedPopulationError', shiftedError, ...
      'maximumTrialStressErrorPa', forceError, 'maximumDetachedMassError', massError, 'trialQueriesPreserveState', pure, ...
      'queries', {queryRows}, 'zeroOverlapRejected', rejectedZero, 'invalidOverlapRejected', rejectedInvalid, ...
      'statePreservedAfterRejectedKinetics', preserved);
    fprintf('Canonical spindle state controls: %s passed\n', name);
  end
  result = struct('version', 'canonical-spindle-state-v1', 'fibers', {records}, ...
    'zeroOverlapKineticsImplemented', false, 'fullSlackDriverExecuted', false);
  write_json(fullfile(package, 'state-controls.json'), result);
end

function write_json(target, result)
  if exist(target, 'file'), error('Preserve previous state controls.'); end
  stream = fopen(target, 'w'); if stream < 0, error('Cannot save state controls.'); end
  cleanup = onCleanup(@() fclose(stream)); fprintf(stream, '%s\n', jsonencode(result));
end

function [hs, parameters] = create_fiber(fiber)
  if strcmp(fiber, 'bag')
    parameters = getDefaultSarcB(); hs = halfSarcWithCoopBag(parameters.f, parameters.g);
  else
    parameters = getDefaultSarcC(); hs = halfSarcWithCoopChain(parameters.f, parameters.g);
  end
  fields = {'power_stroke', 'hs_length', 'cmd_length', 'hsl_slack', 'k_passive', 'compliance_factor'};
  for k = 1:numel(fields), hs.(fields{k}) = parameters.(fields{k}); end
  hs.pCa_perStep = 6.5; hs.update_filamentOverlap(); hs.calcForces(0);
end
