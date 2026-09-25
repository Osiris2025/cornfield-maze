// M25b evidence helper (design-time only, never compiled into the game).
//
// Holds a key down for N seconds — System Events' keystroke presses and releases, which is no good for
// walking, because FarmWalkerController reads Input.GetAxisRaw (a HELD key). JXA + CoreGraphics can post a
// key-down and simply never post the key-up, and it needs no installed tool.
//
//   osascript -l JavaScript scripts/m25b_key_hold.js <keyCode> <seconds>
//
// Key codes: 13 = W, 0 = A, 1 = S, 2 = D, 12 = Q, 14 = E, 56 = left shift.
ObjC.import('CoreGraphics');
ObjC.import('Foundation');

function post(type, code) {
  var ev = $.CGEventCreateKeyboardEvent($(), code, type === 'down');
  $.CGEventPost($.kCGHIDEventTap, ev);
}

function run(argv) {
  var code = Number(argv[0]);
  var seconds = Number(argv[1] || 1);
  post('down', code);
  $.NSThread.sleepForTimeInterval(seconds);
  post('up', code);
  return 'held key ' + code + ' for ' + seconds + 's';
}
