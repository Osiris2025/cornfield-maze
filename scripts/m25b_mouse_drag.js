// M25b evidence helper (design-time only, never compiled into the game).
//
// Drags the mouse by (dx, dy) screen points in small steps, so a plain launch can turn the camera toward
// the moon the way a player does. JavaScript for Automation with the ObjC bridge is used because it is
// part of macOS — no cliclick, no pyobjc, no package to install on Todd's machine.
//
//   osascript -l JavaScript scripts/m25b_mouse_drag.js <dx> <dy> [startX startY]
//
// Prints "dragged dx,dy from x,y to x,y" so the caller can see it actually happened. A drag has to stay
// inside the game window or macOS stops delivering it to the game, so the caller passes the start point.
ObjC.import('CoreGraphics');
ObjC.import('Foundation');

function run(argv) {
  var dx = Math.round(Number(argv[0] || 0));
  var dy = Math.round(Number(argv[1] || 0));
  var steps = Math.max(8, Math.min(40, Math.round(Math.max(Math.abs(dx), Math.abs(dy)) / 20)));

  var start;
  if (argv.length >= 4) {
    start = { x: Number(argv[2]), y: Number(argv[3]) };
  } else {
    start = $.CGEventGetLocation($.CGEventCreate($()));
  }
  var x0 = start.x, y0 = start.y;
  var x1 = x0 + dx, y1 = y0 + dy;

  function post(type, x, y) {
    var ev = $.CGEventCreateMouseEvent($(), type, $.CGPointMake(x, y), $.kCGMouseButtonLeft);
    $.CGEventPost($.kCGHIDEventTap, ev);
  }

  post($.kCGEventMouseMoved, x0, y0);
  $.NSThread.sleepForTimeInterval(0.05);
  post($.kCGEventLeftMouseDown, x0, y0);
  for (var i = 1; i <= steps; i++) {
    post($.kCGEventLeftMouseDragged, x0 + (dx * i) / steps, y0 + (dy * i) / steps);
    $.NSThread.sleepForTimeInterval(0.02);
  }
  post($.kCGEventLeftMouseUp, x1, y1);

  var end = $.CGEventGetLocation($.CGEventCreate($()));
  return 'dragged ' + dx + ',' + dy + ' from ' + x0.toFixed(0) + ',' + y0.toFixed(0) +
         ' to ' + end.x.toFixed(0) + ',' + end.y.toFixed(0);
}
