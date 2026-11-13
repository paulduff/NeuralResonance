function run_spindle_mechanics_reference(package)
% Execute the pinned mechanics equations with a declared Octave options-binding
% copy; preserve the original source and do not alter equations or solver defaults.
% This is a mechanics diagnostic, not a MATLAB-equivalence, afferent, gamma
% motor interface, full slack-driver, physiological or Avatar qualification.
  if exist('OCTAVE_VERSION', 'builtin') ~= 5 || ~strcmp(OCTAVE_VERSION, '11.3.0')
    error('Use the declared isolated Octave 11.3.0 runtime.');
  end
  config = jsondecode(fileread(fullfile(package, 'configuration.json')));
  canonical = isfield(config, 'CanonicalStateCandidate') && config.CanonicalStateCandidate;
  if ~strcmp(config.UpstreamRevision, '721ba03cb775261548766ed1349c4bf2e0e35385')
    error('Unexpected upstream source declaration.');
  end
  output = fullfile(package, 'mechanics');
  if exist(output, 'dir'), error('Preserve previous mechanics evidence.'); end
  mkdir(output);
  restoredefaultpath();
  if ~strcmp(config.RuntimeCompatibility, 'octave-ode23-empty-options-struct-v1') || ~config.ExecutionSourceAdapted
    error('Declare the separate Octave options compatibility copy.');
  end
  executionTree = 'octave-compatible';
  if canonical, executionTree = 'canonical-state'; end
  spindle = fullfile(package, executionTree, 'vendor', 'matlabMuscleSpindleModellingTools', 'spindle_model');
  extrafusal = fullfile(package, executionTree, 'vendor', 'matlabMuscleSpindleModellingTools', 'extrafusal_model');
  addpath(extrafusal); addpath(spindle);
  expectedNames = {'halfSarcWithCoopBag', 'halfSarcWithCoopChain', 'getDefaultSarcB', 'getDefaultSarcC', 'getDefaultSarcE'};
  expectedFiles = {fullfile(spindle, '@halfSarcWithCoopBag', 'halfSarcWithCoopBag.m'), ...
    fullfile(spindle, '@halfSarcWithCoopChain', 'halfSarcWithCoopChain.m'), ...
    fullfile(spindle, 'getDefaultSarcB.m'), fullfile(spindle, 'getDefaultSarcC.m'), fullfile(extrafusal, 'getDefaultSarcE.m')};
  resolved = struct();
  for k = 1:numel(expectedNames)
    path = which(expectedNames{k});
    if ~strcmpi(strrep(path, '\', '/'), strrep(expectedFiles{k}, '\', '/'))
      error('Unexpected upstream function resolution: %s -> %s', expectedNames{k}, path);
    end
    resolved.(expectedNames{k}) = path;
  end
  resolved.ode23 = which('ode23'); resolved.interp1 = which('interp1'); resolved.fzero = which('fzero');
  resolved.odeset = which('odeset');
  core = {'ode23', 'interp1', 'fzero', 'odeset'};
  runtimePrefix = [strrep(config.OctaveHome, '\', '/') '/'];
  for k = 1:numel(core)
    path = strrep(resolved.(core{k}), '\', '/');
    if ~strncmpi(path, runtimePrefix, numel(runtimePrefix))
      error('Numerical function is outside the declared runtime: %s', core{k});
    end
  end
  resolved.griddedInterpolant = which('griddedInterpolant');
  resolved.runtime = OCTAVE_VERSION; resolved.computer = computer();
  resolved.ode23Defaults = odeset();
  resolved.runtimeCompatibility = config.RuntimeCompatibility;
  resolved.canonicalStateCandidate = canonical;
  resolved.emptyOptionsCompatibility = verify_empty_options();
  write_json(fullfile(output, 'resolution.json'), resolved);
  control = resolved.emptyOptionsCompatibility;
  if ~control.emptyArgumentRejected || ~control.threeAndStructExactlyEqual || control.maximumExponentialError > 1e-8
    error('Octave empty-options compatibility controls failed.');
  end

  fibers = {'bag', 'chain'};
  protocols = {'quiet-hold', 'active-hold', 'drive-on-off', 'stretch-shorten', 'imposed-below-slack'};
  steps = [0.001, 0.0005, 0.00025];
  outcomes = {};
  for f = 1:numel(fibers)
    for p = 1:numel(protocols)
      for d = 1:numel(steps)
        label = sprintf('%s-%s-%dus', fibers{f}, protocols{p}, round(1e6 * steps(d)));
        try
          result = trajectory(fibers{f}, protocols{p}, steps(d));
          outcome = struct('label', label, 'completed', result.completed, 'finite', result.finite, 'error', result.error);
        catch failure
          result = struct('error', failure.message, 'stack', failure.stack);
          outcome = struct('label', label, 'completed', false, 'finite', false, 'error', failure.message);
        end
        % MAT retains any nonfinite values even if the JSON encoder writes null.
        save(fullfile(output, [label '.mat']), 'result', '-v7');
        write_json(fullfile(output, [label '.json']), result);
        outcomes{end+1} = outcome;
        fprintf('%s completed=%d finite=%d\n', label, outcome.completed, outcome.finite);
      end
    end
    label = [fibers{f} '-state-boundaries'];
    try
      result = boundaries(fibers{f});
      outcome = struct('label', label, 'completed', true, 'finite', ...
        result.original.finite && result.shifted.finite && result.returned.finite && result.outOfBins.finite && ...
        all(isfinite([result.previousOverlap, result.exactBoundaryFromPositive, result.exactBoundaryFromZero])), 'error', '');
    catch failure
      result = struct('error', failure.message, 'stack', failure.stack);
      outcome = struct('label', label, 'completed', false, 'finite', false, 'error', failure.message);
    end
    save(fullfile(output, [label '.mat']), 'result', '-v7');
    write_json(fullfile(output, [label '.json']), result);
    outcomes{end+1} = outcome;
  end
  summary = struct('outcomes', {outcomes}, 'expectedTrajectories', 30, 'expectedBoundaryRecords', 2, ...
    'upstreamEquationsModified', canonical, 'fullSlackDriverExecuted', false, 'matlabAgreementAssessed', false, ...
    'receptorPotentialComputed', false, 'afferentMembraneExecuted', false, 'physiologyQualified', false, ...
    'avatarRecoveryAssessed', false);
  summary.allExecutionsCompleted = all(cellfun(@(x) x.completed, outcomes));
  summary.allRecordedStatesFinite = all(cellfun(@(x) x.finite, outcomes));
  write_json(fullfile(output, 'execution.json'), summary);
  if ~summary.allExecutionsCompleted || ~summary.allRecordedStatesFinite
    error('Native mechanics execution incomplete or nonfinite; preserve evidence.');
  end
  if canonical
    addpath(fullfile(package, 'source', 'tools'));
    run_spindle_state_controls(package);
  end
end

function result = verify_empty_options()
  % Octave forwards [] as an extra callback argument; an empty options struct
  % selects its default solver settings. Exercise that boundary before fibers.
  derivative = @(t, y) -y;
  [t3, y3] = ode23(derivative, [0, 0.01], 1);
  [t4, y4] = ode23(derivative, [0, 0.01], 1, odeset());
  rejected = false; message = '';
  try
    ode23(derivative, [0, 0.01], 1, []);
  catch failure
    message = failure.message;
    rejected = ~isempty(strfind(message, 'too many inputs'));
  end
  same = isequal(t3, t4) && isequal(y3, y4);
  maximumError = max(abs(y3(:) - exp(-t3(:))));
  result = struct('emptyArgumentRejected', rejected, 'emptyArgumentError', message, ...
    'threeAndStructExactlyEqual', same, 'maximumExponentialError', maximumError, ...
    'timesSeconds', t3(:)', 'states', y3(:)', 'matlabAgreementAssessed', false);
end

function result = trajectory(fiber, protocol, dt)
  [hs, parameters] = create_fiber(fiber);
  count = round(0.1 / dt);
  states = cell(1, count + 1);
  hs.pCa_perStep = drive(protocol, 0);
  hs.update_filamentOverlap(); hs.calcForces(0);
  states{1} = snapshot(hs, 0, 0); % Unevolved state, never an extra t=0 solve.
  finite = states{1}.finite; completedSteps = 0; message = ''; failedAttempt = struct();
  for k = 1:count
    try
      startTime = (k - 1) * dt; endTime = k * dt;
      hs.pCa_perStep = drive(protocol, startTime); % Piecewise constant interval input.
      delta = imposed_length(protocol, endTime) - hs.cmd_length;
      hs.forwardStep(dt, delta, delta, 1, 1);
      states{k+1} = snapshot(hs, k, endTime); finite = finite && states{k+1}.finite;
      completedSteps = k;
    catch failure
      message = failure.message;
      failedAttempt = struct('step', k, 'requestedEndSeconds', k * dt, 'error', message, ...
        'stack', failure.stack, 'y', hs.y, 'binPopulations', hs.bin_pops, 'hsLengthNm', hs.hs_length);
      break; % No retry or invented completion after a failed state transition.
    end
  end
  result = struct('fiber', fiber, 'protocol', protocol, 'dtSeconds', dt, 'durationSeconds', count * dt, ...
    'parameters', parameters, 'snapshots', {states(1:completedSteps+1)}, 'finite', finite, ...
    'completed', completedSteps == count, 'completedSteps', completedSteps, 'error', message, 'failedAttempt', failedAttempt, ...
    'driver', 'isolated-imposed-length-forwardStep', 'timeConvention', 'initial plus completed endpoints', ...
    'slackControllerUsed', false, 'upstreamODEOptionsChanged', false);
end

function [hs, parameters] = create_fiber(fiber)
  if strcmp(fiber, 'bag')
    parameters = getDefaultSarcB(); hs = halfSarcWithCoopBag(parameters.f, parameters.g);
  else
    parameters = getDefaultSarcC(); hs = halfSarcWithCoopChain(parameters.f, parameters.g);
  end
  fields = {'power_stroke', 'hs_length', 'cmd_length', 'hsl_slack', 'k_passive', 'compliance_factor'};
  for k = 1:numel(fields), hs.(fields{k}) = parameters.(fields{k}); end
  % Record original constructor arrays and constants as well as default driver
  % parameters. Do not fill the upstream empty cache fields with invented state.
  parameters.class = class(hs); parameters.x_bins = hs.x_bins;
  parameters.forward_rates = hs.f; parameters.reverse_rates = hs.g;
  parameters.k_cb = hs.k_cb; parameters.cb_number_density = hs.cb_number_density;
  parameters.cb_force_constant = hs.cb_force_constant; parameters.bin_width = hs.bin_width;
  parameters.thick_filament_length = hs.thick_filament_length;
  parameters.thin_filament_length = hs.thin_filament_length; parameters.bare_zone_length = hs.bare_zone_length;
end

function value = drive(protocol, t)
  if strcmp(protocol, 'quiet-hold'), value = 9;
  elseif strcmp(protocol, 'drive-on-off')
    if t >= 0.02 && t < 0.08, value = 6.5; else, value = 9; end
  else, value = 6.5;
  end
end

function value = imposed_length(protocol, t)
  value = 1250;
  if strcmp(protocol, 'stretch-shorten')
    if t > 0.02 && t <= 0.04, value = 1250 + 20 * (t - 0.02) / 0.02;
    elseif t > 0.04 && t <= 0.06, value = 1270;
    elseif t > 0.06 && t <= 0.08, value = 1270 - 20 * (t - 0.06) / 0.02;
    end
  elseif strcmp(protocol, 'imposed-below-slack')
    if t > 0.02 && t <= 0.04, value = 1250 - 250 * (t - 0.02) / 0.02;
    elseif t > 0.04 && t <= 0.06, value = 1000;
    elseif t > 0.06 && t <= 0.08, value = 1000 + 250 * (t - 0.06) / 0.02;
    end
  end
end

function value = snapshot(hs, step, seconds)
  value = struct('step', step, 'timeSeconds', seconds, 'hsLengthNm', hs.hs_length, 'commandLengthNm', hs.cmd_length, ...
    'pCa', hs.pCa_perStep, 'overlap', hs.f_overlap, 'stressPa', hs.hs_force, 'crossBridgeStressPa', hs.cb_force, ...
    'passiveStressPa', hs.passive_force, 'y', hs.y(:)', 'reportedBinPopulations', hs.bin_pops(:)', ...
    'reportedDetached', hs.no_detached, 'reportedBound', hs.f_bound);
  value.yShape = size(hs.y); value.reportedBinShape = size(hs.bin_pops); value.stressShape = size(hs.hs_force);
  attached = hs.y(2:end-2);
  value.attachedFromY = sum(hs.y(2:end-2));
  value.myosinTotal = hs.y(1) + value.attachedFromY;
  value.actinTotal = hs.y(end-1) + hs.y(end);
  value.populationRepresentationDifference = max(abs(attached(:) - hs.bin_pops(:)));
  value.minimumState = min(hs.y(:));
  value.finite = all(isfinite([hs.y(:); hs.bin_pops(:); hs.hs_length; hs.cmd_length; hs.hs_force(:); ...
    hs.cb_force(:); hs.passive_force(:); hs.f_overlap(:); hs.pCa_perStep; hs.no_detached(:); hs.f_bound(:)]));
end

function result = boundaries(fiber)
  [hs, parameters] = create_fiber(fiber); hs.pCa_perStep = 6.5;
  hs.update_filamentOverlap(); hs.calcForces(0);
  for k = 1:400, hs.forwardStep(0.00025, 0, 0, 1, 1); end
  original = snapshot(hs, 400, 0.1);
  % Zero-duration imposed shifts are diagnostic interventions, not natural
  % movement protocols. They expose bookkeeping without another ODE evolution.
  hs.forwardStep(0, 3, 3, 0, 1); shifted = snapshot(hs, 400, 0.1);
  hs.forwardStep(0, -3, -3, 0, 1); returned = snapshot(hs, 400, 0.1);
  hs.forwardStep(0, 100, 100, 0, 1); outOfBins = snapshot(hs, 400, 0.1);
  [first, ~] = create_fiber(fiber); first.pCa_perStep = 9; first.update_filamentOverlap();
  previous = first.f_overlap;
  boundary = first.thick_filament_length + first.thin_filament_length;
  first.hs_length = boundary; first.update_filamentOverlap(); fromPositive = first.f_overlap;
  [second, ~] = create_fiber(fiber); second.pCa_perStep = 9;
  second.hs_length = boundary + 1; second.update_filamentOverlap();
  second.hs_length = boundary; second.update_filamentOverlap(); fromZero = second.f_overlap;
  result = struct('fiber', fiber, 'parameters', parameters, 'original', original, 'shifted', shifted, ...
    'returned', returned, 'outOfBins', outOfBins, 'zeroOverlapLengthNm', boundary, ...
    'previousOverlap', previous, 'exactBoundaryFromPositive', fromPositive, 'exactBoundaryFromZero', fromZero, ...
    'zeroOverlapODEExecuted', false, 'diagnosticInstantaneousShifts', true);
end

function write_json(path, value)
  if exist(path, 'file'), error('Refuse to overwrite saved output: %s', path); end
  text = jsonencode(value); stream = fopen(path, 'w');
  if stream < 0, error('Cannot create output: %s', path); end
  cleanup = onCleanup(@() fclose(stream));
  fprintf(stream, '%s\n', text);
end
