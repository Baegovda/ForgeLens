from Xlib import display, X, protocol
d=display.Display(':77'); r=d.screen().root
WM_DELETE=d.intern_atom('WM_DELETE_WINDOW'); WM_PROTOCOLS=d.intern_atom('WM_PROTOCOLS')
def walk(w):
    for c in w.query_tree().children:
        n=c.get_wm_name()
        if n and 'Forge' in str(n):
            ev=protocol.event.ClientMessage(window=c, client_type=WM_PROTOCOLS, data=(32,[WM_DELETE, X.CurrentTime,0,0,0]))
            c.send_event(ev); print('sent', n)
        walk(c)
walk(r); d.flush()
