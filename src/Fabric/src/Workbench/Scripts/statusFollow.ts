// [mirror] the status page's follow buttons - keep in sync with:
// - src/Client/Bit.Fabric.Client.Web/wwwroot/status-follow.js
/**
 * Stands in for the public status page's one script (src/Client/Bit.Fabric.Client.Web/wwwroot/status-follow.js), which
 * the app runs on its server-rendered page and the Workbench's interactive one never loads: following by push needs a
 * service worker and a real push subscription. A press of the page's own buttons changes the page as that script would,
 * ending the way the view's Following switch says (sw-follow in the address): followed, unless the browser blocks it or
 * it's an iPhone's browser, where a page has to be on the Home Screen first.
 */
export function standInForStatusFollow() {
    function say(text: string | undefined) {
        const note = document.querySelector<HTMLElement>('[data-follow-note]');
        if (note) {
            note.textContent = text ?? '';
            note.hidden = !text;
        }
    }

    function showFollowing(button: HTMLButtonElement, following: boolean) {
        const label = button.querySelector('span');
        if (label) {
            label.textContent = (following ? button.dataset.following : button.dataset.followText) ?? label.textContent;
        }

        button.disabled = following;

        const unfollow = document.querySelector<HTMLElement>('[data-unfollow]');
        if (unfollow) {
            unfollow.hidden = !following;
        }
    }

    document.addEventListener('click', event => {
        const target = event.target instanceof Element ? event.target : null;
        const button = target?.closest<HTMLButtonElement>('[data-follow]');

        if (button) {
            const outcome = new URLSearchParams(location.search).get('sw-follow');

            if (outcome === 'ios') {
                say(button.dataset.ios);
            } else if (outcome === 'denied') {
                say(button.dataset.failed);
            } else {
                say('');
                showFollowing(button, true);
            }

            return;
        }

        if (target?.closest('[data-unfollow]')) {
            const followButton = document.querySelector<HTMLButtonElement>('[data-follow]');
            if (followButton) {
                showFollowing(followButton, false);
                say(followButton.dataset.unfollowed);
            }
        }
    });
}
